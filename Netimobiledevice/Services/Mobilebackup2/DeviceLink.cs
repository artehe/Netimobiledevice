using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Plist;
using Netimobiledevice.Utils;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Services.Mobilebackup2;

/// <summary>
/// Host side of Apple's DeviceLink protocol, as used by <c>com.apple.mobilebackup2</c>: the
/// device drives the session with <c>DLMessage*</c> requests and the host serves them against
/// a local backup directory.
/// </summary>
internal sealed class DeviceLink : IAsyncDisposable {
    // Frame layout: a big-endian uint32 length, then a one-byte code, then payload.
    private const int SizeLength = sizeof(uint);
    private const int CodeLength = sizeof(byte);

    private const int BulkOperationError = -13;

    /// <summary>
    /// Apple's own DeviceLink.framework (the host side Finder uses) sends file data in 32 KiB
    /// frames by default (the "BufferSize" preference under com.apple.DeviceLink;
    /// </summary>
    private const int DownloadChunkSize = 32 * 1024;
    private const string EmptyParameterString = "___EmptyParameterString___";
    private const uint FileTransferTerminator = 0x00;
    /// <summary>
    /// Set the default timeout to be 5 minutes for device link
    /// </summary>
    private const int DeviceLinkTimeout = 5 * 60 * 1000;

    /// <summary>
    /// Relative paths ('/'-separated, normalised) of placeholders left for rejected payloads.
    /// </summary>
    private readonly HashSet<string> _discardedFiles = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Func<ArrayNode, CancellationToken, Task>> _dlHandlers;
    private readonly ILogger _logger;
    private readonly Action<string, string>? _postFileReceive;
    private readonly Func<string, string, bool>? _preserveFile;
    /// <summary>
    /// Last figure answered to DLMessageGetFreeDiskSpace, and the requirement derived from it when
    /// the device follows up with a purge request. See PurgeDiskSpaceAsync().
    /// </summary>
    private long? _reportedFreeSpace;
    /// <summary>
    /// Last figure answered to DLMessageGetFreeDiskSpace, and the requirement derived from it when
    /// the device follows up with a purge request. See PurgeDiskSpaceAsync().
    /// </summary>
    private long? _requiredFreeSpace;
    private readonly string _rootPath;
    private readonly IServiceConnection _service;

    /// <param name="service">Connection to the device's backup service.</param>
    /// <param name="rootPath">Directory the device's paths are resolved against.</param>
    /// <param name="preserveFile">
    /// Optional filter called as <c>(fileName, deviceName)</c> for each uploaded file; returning
    /// <c>false</c> drains the payload and leaves an empty placeholder instead.
    /// </param>
    /// <param name="postFileReceive">
    /// Optional callback <c>(fileName, deviceName)</c> after a file lands, is moved or is copied.
    /// </param>
    /// <param name="logger">Optional logger; defaults to a no-op logger.</param>
    public DeviceLink(
        IServiceConnection service,
        string rootPath,
        Func<string, string, bool>? preserveFile = null,
        Action<string, string>? postFileReceive = null,
        ILogger? logger = null
    ) {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _rootPath = rootPath ?? throw new ArgumentNullException(nameof(rootPath));
        _preserveFile = preserveFile;
        _postFileReceive = postFileReceive;
        _logger = logger ?? NullLogger.Instance;
        _dlHandlers = new Dictionary<string, Func<ArrayNode, CancellationToken, Task>>(StringComparer.Ordinal) {
            [DeviceLinkMessage.ContentsOfDirectory] = ContentsOfDirectoryAsync,
            [DeviceLinkMessage.CopyItem] = CopyItemAsync,
            [DeviceLinkMessage.CreateDirectory] = CreateDirectoryAsync,
            [DeviceLinkMessage.Disconnect] = DisconnectAsync,
            [DeviceLinkMessage.DownloadFiles] = DownloadFilesAsync,
            [DeviceLinkMessage.GetFreeDiskSpace] = GetFreeDiskSpaceAsync,
            [DeviceLinkMessage.PurgeDiskSpace] = PurgeDiskSpaceAsync,
            [DeviceLinkMessage.MoveFiles] = MoveItemsAsync,
            [DeviceLinkMessage.MoveItems] = MoveItemsAsync,
            [DeviceLinkMessage.RemoveFiles] = RemoveItemsAsync,
            [DeviceLinkMessage.RemoveItems] = RemoveItemsAsync,
            [DeviceLinkMessage.UploadFiles] = UploadFilesAsync,
        };

        // Adjust the timeout to be long enough to handle device with a large amount of data
        _service.SetTimeout(DeviceLinkTimeout);
    }

    private static byte[] BuildFrame(Mb2ResultCode code, ReadOnlySpan<byte> payload) {
        byte[] frame = new byte[SizeLength + CodeLength + payload.Length];
        BinaryPrimitives.WriteUInt32BigEndian(frame, (uint) (payload.Length + CodeLength));
        frame[SizeLength] = (byte) code;
        payload.CopyTo(frame.AsSpan(SizeLength + CodeLength));
        return frame;
    }

    private async Task<(int Size, byte Code)> ConsumeFileTransferAsync(
        int size,
        byte code,
        Stream? destination,
        CancellationToken ct
    ) {
        while (size != 0 && code == (byte) Mb2ResultCode.FileData) {
            byte[] chunk = await _service.ReceiveAsync(size, ct).ConfigureAwait(false);
            if (destination is not null) {
                await destination.WriteAsync(chunk, ct).ConfigureAwait(false);
            }

            (size, code) = await ReceiveFrameHeaderAsync(ct).ConfigureAwait(false);
        }

        return (size, code);
    }

    private async Task ContentsOfDirectoryAsync(ArrayNode message, CancellationToken ct) {
        string path = FullPath(message[1].AsStringNode().Value);

        DictionaryNode data = [];
        foreach (string entry in Directory.EnumerateFileSystemEntries(path)) {
            if (ct.IsCancellationRequested) {
                break;
            }

            bool isDirectory = Directory.Exists(entry);
            bool isFile = File.Exists(entry);
            string fileType = isFile ? DeviceLinkMessage.FileTypeRegular
                : isDirectory ? DeviceLinkMessage.FileTypeDirectory
                : DeviceLinkMessage.FileTypeUnknown;

            // The modified time is shifted back by the Apple epoch and expressed as a naive local time.
            DateTime modified = isDirectory ? Directory.GetLastWriteTimeUtc(entry) : File.GetLastWriteTimeUtc(entry);
            DateTime shifted = DateTime.SpecifyKind(modified.Add(-TimeSpan.FromTicks(DateNode.AppleEpoch.Ticks)).ToLocalTime(), DateTimeKind.Unspecified);

            data[Path.GetFileName(entry)] = new DictionaryNode {
                [DeviceLinkMessage.FileType] = new StringNode(fileType),
                [DeviceLinkMessage.FileSize] = new IntegerNode(isFile ? new FileInfo(entry).Length : 0L),
                [DeviceLinkMessage.FileModificationDate] = new DateNode(shifted),
            };
        }

        await StatusResponseAsync(0, statusDict: data, ct: ct).ConfigureAwait(false);
    }

    private void CopyDiscardedFiles(string source, string destination, bool isDirectory) {
        string normalizedSource = NormalizeRelative(source);
        string normalizedDestination = NormalizeRelative(destination);
        foreach (string path in DiscardedFilesBelow(source, isDirectory)) {
            _discardedFiles.Add(Rebase(path, normalizedSource, normalizedDestination));
        }
    }

    private async Task CopyItemAsync(ArrayNode message, CancellationToken ct) {
        string srcRelative = message[1].AsStringNode().Value;
        string dstRelative = message[2].AsStringNode().Value;

        string src = FullPath(srcRelative);
        if (!PathExists(src) && _preserveFile is not null) {
            await StatusResponseAsync(0, ct: ct).ConfigureAwait(false);
            return;
        }

        string dest = FullPath(dstRelative);
        CreateParentDirectory(dest);

        bool isDirectory = Directory.Exists(src);
        if (isDirectory) {
            CopyTree(src, dest);
        }
        else {
            FileInfo source = new FileInfo(src);
            FileInfo target = new FileInfo(Directory.Exists(dest) ? Path.Combine(dest, Path.GetFileName(src)) : dest);
            source.CopyTo(target.FullName, true);
        }

        CopyDiscardedFiles(srcRelative, dstRelative, isDirectory);
        _postFileReceive?.Invoke(dstRelative, srcRelative);
        await StatusResponseAsync(0, ct: ct).ConfigureAwait(false);
    }

    private static void CopyTree(string source, string destination) {
        if (PathExists(destination)) {
            throw new IOException($"Destination path '{destination}' already exists");
        }

        Directory.CreateDirectory(destination);
        foreach (string entry in Directory.EnumerateFileSystemEntries(source)) {
            string target = Path.Combine(destination, Path.GetFileName(entry));
            if (Directory.Exists(entry)) {
                CopyTree(entry, target);
            }
            else {
                File.Copy(entry, target);
                File.SetLastWriteTimeUtc(target, File.GetLastWriteTimeUtc(entry));
            }
        }

        Directory.SetLastWriteTimeUtc(destination, Directory.GetLastWriteTimeUtc(source));
    }

    private async Task CreateDirectoryAsync(ArrayNode message, CancellationToken ct) {
        string newDirPath = FullPath(message[1].AsStringNode().Value);
        Directory.CreateDirectory(newDirPath);
        await StatusResponseAsync(0, ct: ct).ConfigureAwait(false);
    }

    private static void CreateParentDirectory(string path) {
        string? parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(parent)) {
            Directory.CreateDirectory(parent);
        }
    }

    private static void DeleteFileIfExists(string path) {
        if (File.Exists(path)) {
            try {
                File.Delete(path);
            }
            catch (DirectoryNotFoundException) {
                // This is fine :)
            }
        }
    }

    /// <summary>
    /// Recover the free space the device demands from a <c>CACHE_DELETE_AMOUNT</c>.
    /// </summary>
    /// <returns>
    /// <c>null</c> when the figures don't fit the model documented in
    /// <see cref="PurgeDiskSpaceAsync"/> - an unknown reported figure, a non-integer amount, or an
    /// amount below the 2 GiB overshoot that the device always adds.
    /// </returns>
    private long? DeriveRequiredFreeSpace(long? amount) {
        const long PurgeRequestOvershoot = 0x80000000;

        if (_reportedFreeSpace is not long reported || amount is null) {
            return null;
        }

        if (amount < PurgeRequestOvershoot) {
            return null;
        }

        return amount + reported - PurgeRequestOvershoot;
    }

    /// <summary>Return the discarded-placeholder entries at or below <paramref name="path"/>.</summary>
    private List<string> DiscardedFilesBelow(string path, bool isDirectory) {
        string normalized = NormalizeRelative(path);
        if (!isDirectory) {
            return _discardedFiles.Contains(normalized) ? [normalized] : [];
        }
        return [
            .. _discardedFiles.Where(candidate =>
                normalized.Length == 0 ||
                candidate == normalized ||
                (candidate.Length > normalized.Length &&
                    candidate.StartsWith(normalized, StringComparison.Ordinal) &&
                    candidate[normalized.Length] == '/'
                )
            )
        ];
    }

    private async Task DisconnectAsync(ArrayNode _, CancellationToken cancellationToken) {
        ArrayNode message = [
            new StringNode(DeviceLinkMessage.Disconnect),
            new StringNode(EmptyParameterString)
        ];
        await _service.SendPlistAsync(message, PlistFormat.Binary, cancellationToken);
    }

    private async Task DownloadFilesAsync(ArrayNode message, CancellationToken ct) {
        DictionaryNode status = [];
        byte[] frame = new byte[SizeLength + CodeLength + DownloadChunkSize];

        foreach (PropertyNode fileNode in message[1].AsArrayNode()) {
            if (fileNode is not StringNode fsnode) {
                throw new FormatException($"Unexpected node type. Expected StringNode but got {fileNode.NodeType}");
            }
            string file = fsnode.Value;

            // Get the path value(s)
            byte[] fileBytes = Encoding.UTF8.GetBytes(file);
            byte[] namePrefix = new byte[SizeLength + fileBytes.Length];
            BinaryPrimitives.WriteInt32BigEndian(namePrefix, fileBytes.Length);
            fileBytes.CopyTo(namePrefix, SizeLength);
            await _service.SendAsync(namePrefix, ct).ConfigureAwait(false);

            // Only file-system failures are reported to the device; transport errors propagate.
            int? fileError = null;
            FileStream? stream = null;
            string filePath = FullPath(file);

            if (!File.Exists(filePath) && !Directory.Exists(filePath)) {
                fileError = Errno.ENOENT;
            }
            else if (Directory.Exists(filePath)) {
                fileError = Errno.EISDIR;
            }

            if (fileError is null) {
                try {
                    stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 0, useAsync: true);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
                    fileError = Errno.EIO;
                }
            }

            if (stream is not null) {
                await using (stream.ConfigureAwait(false)) {
                    while (true) {
                        int read;
                        try {
                            read = await stream.ReadAsync(frame.AsMemory(SizeLength + CodeLength, DownloadChunkSize), ct).ConfigureAwait(false);
                        }
                        catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
                            fileError = Errno.EIO;
                            break;
                        }

                        if (read == 0) {
                            break;
                        }

                        BinaryPrimitives.WriteInt32BigEndian(frame, (int) (read + CodeLength));
                        frame[SizeLength] = (byte) Mb2ResultCode.FileData;
                        await _service.SendAsync(frame.AsMemory(0, SizeLength + CodeLength + read), ct).ConfigureAwait(false);
                    }
                }
            }

            if (fileError is null) {
                await _service.SendAsync(BuildFrame(Mb2ResultCode.Success, []), ct).ConfigureAwait(false);
            }
            else {
                if (_logger.IsEnabled(LogLevel.Debug)) {
                    _logger.LogDebug("Failed to send {file} to the device, with errno {fileError}", file, fileError);
                }

                string strError = Errno.GetDeviceErrorString(fileError ?? -1);
                status[file] = new DictionaryNode() {
                    [DeviceLinkMessage.FileErrorString] = new StringNode(strError),
                    [DeviceLinkMessage.FileErrorCode] = new IntegerNode(Errno.GetDeviceError(fileError ?? -1))
                };
                await _service.SendAsync(BuildFrame(Mb2ResultCode.LocalError, Encoding.UTF8.GetBytes(strError)), ct).ConfigureAwait(false);
            }
        }

        await _service.SendAsync(BitConverter.GetBytes(FileTransferTerminator), ct).ConfigureAwait(false);
        if (status.Count > 0) {
            await StatusResponseAsync(BulkOperationError, "Multi status", status, ct).ConfigureAwait(false);
        }
        else {
            await StatusResponseAsync(0, ct: ct).ConfigureAwait(false);
        }
    }

    private void ForgetDiscardedFiles(string path, bool isDirectory) => _discardedFiles.ExceptWith(DiscardedFilesBelow(path, isDirectory));

    private string FullPath(string relative) => Path.Combine(_rootPath, relative);

    private async Task GetFreeDiskSpaceAsync(ArrayNode message, CancellationToken ct) {
        // On Unix DriveInfo accepts any path; on Windows it needs the volume root.
        string target = OperatingSystem.IsWindows() ? Path.GetPathRoot(Path.GetFullPath(_rootPath)) ?? _rootPath : _rootPath;
        long freeSpace = new DriveInfo(target).AvailableFreeSpace;

        if (_logger.IsEnabled(LogLevel.Debug)) {
            _logger.LogDebug("Reporting {FreeSpace} bytes free at {RootPath}", freeSpace, _rootPath);
        }

        _reportedFreeSpace = freeSpace;
        IntegerNode freeSpaceNode = new IntegerNode(freeSpace);

        await StatusResponseAsync(0, statusDict: freeSpaceNode, ct: ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Describe an <c>MBErrorDomain/105</c> refusal in terms of the bytes involved.
    /// </summary>
    /// <remarks>
    /// The device only reaches this after the host has answered its purge request, so
    /// <see cref="PurgeDiskSpaceAsync"/> has normally already recovered the threshold the device is
    /// comparing against. Without it - an unrecognised amount, or a 105 that arrived with no
    /// preceding purge - fall back to the device's own wording.
    /// </remarks>
    private string InsufficientDiskSpaceError(DictionaryNode response) {
        const string detail =
            " The device counts every hardlink and clone at full size, because each is stored as a " +
            "separate file in the backup, so this can far exceed the data stored on the device.";

        if (_requiredFreeSpace is not long required || _reportedFreeSpace is not long reported) {
            return $"Device refused the backup: {PropertyList.SaveAsString(response, Plist.PlistFormat.Xml)}.{detail}";
        }

        return string.Create(CultureInfo.InvariantCulture,
            $"Device refused the backup: it needs more than {required} bytes free " +
            $"at {_rootPath}, host reported {reported} " +
            $"({required + 1 - reported} more needed) " +
            $"(MBErrorDomain/{Mb2ResultCode.InsufficientDiskSpace}).{detail}");
    }

    private void MoveDiscardedFiles(string source, string destination, bool isDirectory) {
        List<string> discarded = DiscardedFilesBelow(source, isDirectory);
        _discardedFiles.ExceptWith(discarded);
        string normalizedSource = NormalizeRelative(source);
        string normalizedDestination = NormalizeRelative(destination);
        foreach (string path in discarded) {
            _discardedFiles.Add(Rebase(path, normalizedSource, normalizedDestination));
        }
    }

    private async Task MoveItemsAsync(ArrayNode message, CancellationToken ct) {
        foreach ((string src, PropertyNode destinationNode) in message[1].AsDictionaryNode()) {
            string source = FullPath(src);
            if (!PathExists(source) && _preserveFile is not null) {
                continue;
            }

            if (destinationNode is not StringNode dstStringNode) {
                continue;
            }
            string dst = dstStringNode.Value;

            string dest = FullPath(dst);
            CreateParentDirectory(dest);

            if (File.Exists(source)) {
                FileInfo file = new FileInfo(source);
                file.MoveTo(dest);
            }
            else if (Directory.Exists(source)) {
                DirectoryInfo dir = new DirectoryInfo(source);
                dir.MoveTo(dest);
            }

            // the source is gone after the move; the destination mirrors its kind
            MoveDiscardedFiles(src, dst, isDirectory: Directory.Exists(dest));
            _postFileReceive?.Invoke(dst, src);
        }

        await StatusResponseAsync(0, ct: ct).ConfigureAwait(false);
    }

    private static string NormalizeRelative(string path) => string.Join(
        '/',
        path.Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(part => part != ".")
    );

    private static bool PathExists(string path) => File.Exists(path) || Directory.Exists(path);

    private async Task<byte[]> PrefixedReceiveAsync(CancellationToken ct) {
        byte[] sizeBytes = await _service.ReceiveAsync(SizeLength, ct).ConfigureAwait(false);

        int size = BinaryPrimitives.ReadInt32BigEndian(sizeBytes);
        if (size < 0 || size > int.MaxValue) {
            throw new DeviceLinkException($"Invalid DeviceLink frame size: {size}");
        }

        return await _service.ReceiveAsync(size, ct).ConfigureAwait(false);
    }

    /// <summary>Answer the device's request that the host free up disk space.</summary>
    private async Task PurgeDiskSpaceAsync(ArrayNode message, CancellationToken ct) {
        long? amount = message.Count > 1 ? message[1].AsIntegerNode().SignedValue : null;
        long? urgency = message.Count > 2 ? message[2].AsIntegerNode().SignedValue : null;

        _requiredFreeSpace = DeriveRequiredFreeSpace(amount);
        if (_requiredFreeSpace is not long required) {
            _logger.LogWarning(
                "device asked the host to free {Amount} bytes under {Urgency} at {RootPath}; no purge backend available",
                amount,
                urgency,
                _rootPath
            );
        }
        else {
            long reported = _reportedFreeSpace ?? -1;
            _logger.LogWarning(
                "device needs more than {Required} bytes free at {RootPath} to back up; host reported {Reported} " +
                "({Missing} more needed). No purge backend available, so the device decides how to proceed",
                required,
                _rootPath,
                reported,
                required + 1 - reported
            );
            if (_logger.IsEnabled(LogLevel.Debug)) {
                _logger.LogDebug("{Purge}: amount={Amount} urgency={Urgency}",
                    DeviceLinkMessage.PurgeDiskSpace,
                    amount,
                    urgency
                );
            }
        }

        await StatusResponseAsync(
            -1,
            $"{DeviceLinkMessage.PurgeDiskSpace} failed to purge",
            statusDict: new IntegerNode(0),
            ct: ct
        ).ConfigureAwait(false);
    }

    private static string Rebase(string path, string source, string destination) {
        string remainder = source.Length == 0 ? path : path[source.Length..].TrimStart('/');
        if (remainder.Length == 0) {
            return destination;
        }
        return destination.Length == 0 ? remainder : $"{destination}/{remainder}";
    }

    private async Task<(int Size, byte Code)> ReceiveFrameHeaderAsync(CancellationToken ct) {
        byte[] sizeBytes = await _service.ReceiveAsync(SizeLength, ct).ConfigureAwait(false);
        byte[] codeBytes = await _service.ReceiveAsync(CodeLength, ct).ConfigureAwait(false);
        int size = BinaryPrimitives.ReadInt32BigEndian(sizeBytes) - CodeLength;
        return (size, codeBytes[0]);
    }

    private async Task RemoveItemsAsync(ArrayNode message, CancellationToken ct) {
        foreach (PropertyNode pathNode in message[1].AsArrayNode()) {
            if (pathNode is not StringNode pathStringNode) {
                continue;
            }
            string path = pathStringNode.Value;

            string removePath = FullPath(path);

            bool isDirectory = Directory.Exists(removePath);
            if (isDirectory) {
                Directory.Delete(removePath, recursive: true);
            }
            else {
                DeleteFileIfExists(removePath);
            }

            ForgetDiscardedFiles(path, isDirectory);
        }

        await StatusResponseAsync(0, ct: ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a status report to the backup service.
    /// </summary>
    /// <param name="statusCode">The error code to send (as errno value).</param>
    /// <param name="statusStr">An optional error message to send.</param>
    /// <param name="statusDict">An optional DictionaryNode with additional value(s).</param>
    private ValueTask StatusResponseAsync(
        long statusCode,
        string? statusStr = null,
        PropertyNode? statusDict = null,
        CancellationToken ct = default
    ) => _service.SendPlistAsync(
        new ArrayNode() {
            new StringNode(DeviceLinkMessage.MessageStatusResponse),
            new IntegerNode(statusCode),
            string.IsNullOrEmpty(statusStr) ? new StringNode(EmptyParameterString) : new StringNode(statusStr),
            statusDict ?? new DictionaryNode()
        },
        PlistFormat.Binary,
        ct
    );

    private async Task UploadFilesAsync(ArrayNode message, CancellationToken ct) {
        while (true) {
            string deviceName = Encoding.UTF8.GetString(await PrefixedReceiveAsync(ct).ConfigureAwait(false));
            if (deviceName.Length == 0) {
                break;
            }

            string fileName = Encoding.UTF8.GetString(await PrefixedReceiveAsync(ct).ConfigureAwait(false));
            (int size, byte code) = await ReceiveFrameHeaderAsync(ct).ConfigureAwait(false);

            bool shouldPreserve = _preserveFile?.Invoke(fileName, deviceName) ?? true;
            if (shouldPreserve) {
                FileStream destination = new(FullPath(fileName), FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 0, useAsync: true);
                await using (destination.ConfigureAwait(false)) {
                    (size, code) = await ConsumeFileTransferAsync(size, code, destination, ct).ConfigureAwait(false);
                }
            }
            else {
                string path = FullPath(fileName);

                CreateParentDirectory(path);
                if (File.Exists(path)) {
                    File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
                }
                else {
                    using (File.Create(path)) { }
                }

                _discardedFiles.Add(NormalizeRelative(fileName));
                (size, code) = await ConsumeFileTransferAsync(size, code, null, ct).ConfigureAwait(false);
            }

            if (code == (byte) Mb2ResultCode.RemoteError) {
                // iOS 17 beta devices give this error for: backup_manifest.db
                string errorMessage = Encoding.UTF8.GetString(await _service.ReceiveAsync(size, ct).ConfigureAwait(false));
                _logger.LogWarning(
                    "Failed to fully upload: {FileName}. Device file name: {DeviceName}. Reason: {Reason}",
                    fileName,
                    deviceName,
                    errorMessage
                );
                continue;
            }

            if (code != (byte) Mb2ResultCode.Success) {
                throw new DeviceLinkException($"Unexpected DeviceLink frame code {code} while receiving {fileName}");
            }

            _postFileReceive?.Invoke(fileName, deviceName);
        }

        await StatusResponseAsync(0, ct: ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Remove temporary placeholders created for payloads rejected by a backup filter.
    /// </summary>
    public void CleanupDiscardedFiles() {
        string root = Path.TrimEndingDirectorySeparator(_rootPath);
        foreach (string relativePath in _discardedFiles.OrderByDescending(p => p.Split('/').Length)) {
            string path = FullPath(relativePath);
            DeleteFileIfExists(path);

            string? parent = Path.GetDirectoryName(path);
            while (parent is not null && !string.Equals(Path.TrimEndingDirectorySeparator(parent), root, StringComparison.Ordinal)) {
                bool directoryIsEmpty = Directory.Exists(parent) && !Directory.EnumerateFileSystemEntries(parent).Any();
                if (!directoryIsEmpty) {
                    break;
                }

                try {
                    Directory.Delete(parent);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
                    break;
                }

                parent = Path.GetDirectoryName(parent);
            }
        }
        _discardedFiles.Clear();
    }

    public async ValueTask DisposeAsync() {
        await DisconnectAsync([], CancellationToken.None);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Serve device requests until it sends <c>DLMessageProcessMessage</c>, and return that
    /// message's <c>Content</c>.
    /// </summary>
    /// <param name="progressCallback">Receives the progress value carried by bulk file operations.</param>
    /// <exception cref="NotEnoughDiskSpaceException">The device refused the backup for lack of host space.</exception>
    /// <exception cref="DeviceLinkException">The device reported any other error.</exception>
    public async Task<Mb2ResultCode> DlLoopAsync(ProgressCallback? progressCallback = null, CancellationToken cancellationToken = default) {
        ProgressCallback callback = progressCallback ?? (static _ => { });

        while (true) {
            ArrayNode message = await ReceiveMessageAsync(cancellationToken).ConfigureAwait(false);

            string command = message.Count > 0 && message[0].AsStringNode().Value is string s
                ? s
                : throw new DeviceLinkException($"Malformed DeviceLink message: {PropertyList.SaveAsString(message, PlistFormat.Xml)}");
            if (_logger.IsEnabled(LogLevel.Debug)) {
                _logger.LogDebug("Command recieved: {command}", command);
            }

            switch (command) {
                case DeviceLinkMessage.DownloadFiles:
                case DeviceLinkMessage.MoveFiles:
                case DeviceLinkMessage.MoveItems:
                case DeviceLinkMessage.RemoveFiles:
                case DeviceLinkMessage.RemoveItems: {
                    callback(message[3].AsRealNode().Value);
                    break;
                }

                case DeviceLinkMessage.UploadFiles: {
                    callback(message[2].AsRealNode().Value);
                    break;
                }
            }

            if (command == DeviceLinkMessage.ProcessMessage) {
                DictionaryNode response = message[1].AsDictionaryNode();
                long errorCode = response["ErrorCode"].AsIntegerNode().SignedValue;
                if (errorCode == 0) {
                    return Mb2ResultCode.Success;
                }

                if ((Mb2ResultCode) errorCode == Mb2ResultCode.InsufficientDiskSpace) {
                    throw new NotEnoughDiskSpaceException(InsufficientDiskSpaceError(response));
                }
                throw new DeviceLinkException($"Device link error: {PropertyList.SaveAsString(message[1], Plist.PlistFormat.Xml)}");
            }

            if (!_dlHandlers.TryGetValue(command, out Func<ArrayNode, CancellationToken, Task>? handler)) {
                throw new DeviceLinkException($"Unhandled DeviceLink command: {command}");
            }
            await handler(message, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<ArrayNode> ReceiveMessageAsync(CancellationToken cancellationToken) {
        PropertyNode? responseNode = await _service.ReceivePlistAsync(cancellationToken).ConfigureAwait(false);
        if (responseNode is not ArrayNode plist) {
            string plistStr = "null";
            if (responseNode is not null) {
                plistStr = PropertyList.SaveAsString(responseNode, Plist.PlistFormat.Xml);
            }
            throw new DeviceLinkException($"Expected a DeviceLink message array, got: {plistStr}");
        }
        return plist;
    }

    public ValueTask SendProcessMessageAsync(
        PropertyNode message,
        CancellationToken cancellationToken = default
    ) => _service.SendPlistAsync(
        new ArrayNode() {
            new StringNode(DeviceLinkMessage.ProcessMessage),
            message
        },
        PlistFormat.Binary,
        cancellationToken
    );

    /// <summary>
    /// Performs the DLMessageVersionExchange with the connected device. 
    /// This should be the first operation to be executed by an implemented
    /// device link service client.
    /// </summary>
    public async Task VersionExchangeAsync(ulong versionMajor, ulong versionMinor, CancellationToken cancellationToken = default) {
        ArrayNode versionExchange = await ReceiveMessageAsync(cancellationToken).ConfigureAwait(false);
        if (versionExchange.Count < 3) {
            throw new DeviceLinkException($"{DeviceLinkMessage.VersionExchange} has unexpected format (size < 3)");
        }

        string dlMessage = versionExchange[0].AsStringNode().Value;
        if (string.IsNullOrEmpty(dlMessage) || dlMessage != DeviceLinkMessage.VersionExchange) {
            throw new DeviceLinkException($"Didn't receive {DeviceLinkMessage.VersionExchange} from device");
        }

        ulong vMajor = versionExchange[1].AsIntegerNode().Value;
        ulong vMinor = versionExchange[2].AsIntegerNode().Value;
        if (vMajor > versionMajor) {
            throw new DeviceLinkException($"Version mismatch detected received {vMajor}.{vMinor}, expected {versionMajor}.{versionMinor}");
        }
        else if (vMajor == versionMajor && vMinor > versionMinor) {
            throw new DeviceLinkException($"Version mismatch detected received {vMajor}.{vMinor}, expected {versionMajor}.{versionMinor}");
        }

        await _service.SendPlistAsync(
            new ArrayNode {
                new StringNode(DeviceLinkMessage.VersionExchange),
                new StringNode(DeviceLinkMessage.VersionsOk),
                new IntegerNode(versionMajor)
            },
            PlistFormat.Binary,
            cancellationToken
        ).ConfigureAwait(false);

        ArrayNode deviceReady = await ReceiveMessageAsync(cancellationToken).ConfigureAwait(false);
        if (deviceReady.Count == 0 || deviceReady[0].AsStringNode().Value != DeviceLinkMessage.Ready) {
            throw new DeviceLinkException("Device link didn't return ready state");
        }
    }
}

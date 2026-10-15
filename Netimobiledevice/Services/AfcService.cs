using Microsoft.Extensions.Logging;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Services.Afc;
using Netimobiledevice.Utils;
using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Services;

/// <summary>
/// Apple File Connection (AFC) service for iOS device file system access.
/// </summary>
/// <remarks>
/// Gives full access to <c>/var/mobile/Media</c>: read, write, delete, rename and directory operations.
/// </remarks>
/// <param name="lockdown">Provider used to start the service.</param>
/// <param name="serviceName">Optional override; auto-detected from the provider type when null.</param>
/// <param name="logger">Optional log sink; warnings and errors go to stderr by default.</param>
public class AfcService(
    LockdownServiceProvider lockdown,
    string? serviceName = null,
    ILogger? logger = null
) : LockdownService(
    lockdown,
    serviceName ?? (lockdown is LockdownClient ? LockdownServiceName : RemoteServiceName),
    logger: logger
) {
    /// <summary>4 MB.</summary>
    private const int MaximumReadSize = 4 * 1024 * 1024;
    private const int MaximumWriteSize = 1 << 30;

    /// <summary>Service identifier for lockdown (usbmux) connections.</summary>
    public const string LockdownServiceName = "com.apple.afc";
    /// <summary>Service identifier for RSD connections.</summary>
    public const string RemoteServiceName = "com.apple.afc.shim.remote";

    private readonly ConcurrentDictionary<ulong, TaskCompletionSource<(AfcError Status, byte[] Data)>> _pending = new();
    private readonly Lock _readerLock = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private ulong _packetNumber;
    private Task? _readerTask;
    private CancellationTokenSource? _readerCts;
    /// <summary>
    /// Completed (never reset) once the connection terminates; the reader task itself is not a stable
    /// signal because SendAndWaitAsync restarts exited readers.
    /// </summary>
    private readonly TaskCompletionSource _terminated = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Counter used for packet sequence numbers.</summary>
    public ulong PacketNumber => Interlocked.Read(ref _packetNumber);

    private async Task DispatchPacketAsync(AfcOpCode operation, ReadOnlyMemory<byte> data, ulong thisLength) {
        ulong packetNumber = _packetNumber;
        byte[] packet = AfcProtocol.BuildPacket(packetNumber, operation, data.Span, thisLength);
        Interlocked.Increment(ref _packetNumber);

        if (Service is null) {
            throw new AfcException("Service is null");
        }
        await Service.SendAsync(packet).ConfigureAwait(false);
    }

    private void EnsureReader() {
        lock (_readerLock) {
            if (_readerTask is { IsCompleted: false }) {
                return;
            }

            _readerCts?.Dispose();
            _readerCts = new CancellationTokenSource();

            CancellationToken token = _readerCts.Token;
            _readerTask = Task.Run(() => ReaderLoopAsync(token));
        }
    }

    private void FailAllPending(Exception exception) {
        foreach (ulong key in _pending.Keys) {
            if (_pending.TryRemove(key, out TaskCompletionSource<(AfcError Status, byte[] Data)>? waiter)) {
                waiter.TrySetException(exception);
            }
        }
    }

    /// <summary>
    ///  A leftmost search finds a match at index 0 whenever one exists there.
    /// </summary>
    /// <param name="regex"></param>
    /// <param name="input"></param>
    /// <returns></returns>
    private static bool MatchesAtStart(Regex regex, string input) {
        Match m = regex.Match(input);
        return m.Success && m.Index == 0;
    }

    private async Task PushInternalAsync(
        string localPath,
        string remotePath,
        Func<string, string, ValueTask>? callback,
        CancellationToken cancellationToken
    ) {
        if (callback is not null) {
            await callback(localPath, remotePath).ConfigureAwait(false);
        }

        if (!Directory.Exists(localPath)) {
            // Normal file
            try {
                if (await IsDirAsync(remotePath, cancellationToken).ConfigureAwait(false)) {
                    // Remote is dir.
                    remotePath = PosixPath.Join(remotePath, Path.GetFileName(localPath));
                }
            }
            catch (AfcFileNotFoundException) {
                // Remote is file.
                string remoteParent = PosixPath.DirName(remotePath);
                if (!await ExistsAsync(remoteParent, cancellationToken).ConfigureAwait(false)) {
                    throw;
                }
                remotePath = PosixPath.Join(remoteParent, PosixPath.BaseName(remotePath));
            }

            long srcSize = new FileInfo(localPath).Length;
            if (srcSize <= MaximumReadSize) {
                byte[] contents = await File.ReadAllBytesAsync(localPath, cancellationToken).ConfigureAwait(false);
                await SetFileContentsAsync(remotePath, contents, cancellationToken).ConfigureAwait(false);
            }
            else {
                // Stream large files in chunks instead of loading them whole into memory.
                ulong handle = await FileOpenAsync(
                    remotePath,
                    AfcFileOpenMode.WriteOnly,
                    cancellationToken
                ).ConfigureAwait(false);
                try {
                    await using (FileStream f = new FileStream(
                        localPath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read)
                    ) {
                        byte[] buffer = new byte[MaximumReadSize];
                        while (true) {
                            int read = await f.ReadAtLeastAsync(
                                buffer,
                                buffer.Length,
                                throwOnEndOfStream: false,
                                cancellationToken
                            ).ConfigureAwait(false);
                            if (read == 0) {
                                break;
                            }

                            await FileWriteAsync(
                                handle,
                                buffer.AsMemory(0, read),
                                cancellationToken: cancellationToken
                            ).ConfigureAwait(false);
                        }
                    }
                }
                finally {
                    await FileCloseAsync(handle, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        else {
            // directory
            if (!await ExistsAsync(remotePath, cancellationToken).ConfigureAwait(false)) {
                await MakeDirsAsync(remotePath, cancellationToken).ConfigureAwait(false);
            }

            foreach (string localFilename in Directory.EnumerateFileSystemEntries(localPath)) {
                string filename = Path.GetFileName(localFilename);
                string remoteFilename = PosixPath.Join(remotePath, filename);
                if (remoteFilename.StartsWith('/')) {
                    remoteFilename = remoteFilename[1..];
                }

                if (Directory.Exists(localFilename) &&
                    !await ExistsAsync(remoteFilename, cancellationToken).ConfigureAwait(false)
                ) {
                    await MakeDirsAsync(remoteFilename, cancellationToken).ConfigureAwait(false);
                }

                await PushInternalAsync(
                    localFilename,
                    remoteFilename,
                    callback,
                    cancellationToken
                ).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Background task: read response packets and hand each to the waiter for its packet number.
    /// </summary>
    private async Task ReaderLoopAsync(CancellationToken cancellationToken) {
        try {
            while (true) {
                if (Service is null) {
                    throw new AfcException("Service is null");
                }

                byte[] headerBytes = await Service.ReceiveAsync(AfcProtocol.HeaderSize, cancellationToken).ConfigureAwait(false);
                if (headerBytes.Length == 0) {
                    FailAllPending(new ConnectionTerminatedException());
                    break;
                }

                AfcHeader header = AfcProtocol.ParseHeader(headerBytes);
                if (header.EntireLength < AfcProtocol.HeaderSize) {
                    throw new AfcException($"Invalid AFC packet length {header.EntireLength}");
                }

                int length = checked((int) (header.EntireLength - AfcProtocol.HeaderSize));
                byte[] data = length > 0 ? await Service.ReceiveAsync(length, cancellationToken).ConfigureAwait(false) : [];

                AfcError status = AfcError.Success;
                if (header.Operation == AfcOpCode.Status) {
                    if (length != 8) {
                        Logger.LogError("Status length != 8");
                    }

                    status = AfcProtocol.ParseStatus(data);
                }
                else if (header.Operation != AfcOpCode.Data && Logger.IsEnabled(LogLevel.Debug)) {
                    Logger.LogDebug("Unexpected AFC opcode {operation}", header.Operation);
                }

                if (_pending.TryRemove(header.PacketNum, out TaskCompletionSource<(AfcError Status, byte[] Data)>? waiter)) {
                    waiter.TrySetResult((status, data));
                }
                else {
                    Logger.LogWarning("AFC: no waiter for packet_num={packetNumber}", header.PacketNum);
                }
            }
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested) {
            // Shutting down; CloseAsync fails the pending waiters.
        }
        catch (Exception e) {
            // Propagate to all pending waiters so they don't hang.
            FailAllPending(e);
        }
        finally {
            _terminated.TrySetResult();
        }
    }

    private async Task WriteChunkAsync(
        byte[] fileHandle,
        ReadOnlyMemory<byte> chunk,
        string error,
        CancellationToken cancellationToken
    ) {
        byte[] payload = new byte[fileHandle.Length + chunk.Length];
        fileHandle.CopyTo(payload, 0);
        chunk.Span.CopyTo(payload.AsSpan(fileHandle.Length));
        (AfcError status, byte[] _) = await SendAndWaitAsync(
            AfcOpCode.FileWrite,
            payload,
            thisLength: 48,
            cancellationToken
        ).ConfigureAwait(false);
        if (status != AfcError.Success) {
            throw new AfcException($"{error}: {status}", status);
        }
    }

    /// <summary>
    /// Run one operation and check its status. Throws <see cref="AfcFileNotFoundException"/> for
    /// <c>OBJECT_NOT_FOUND</c> and <see cref="AfcException"/> for any other failure.
    /// </summary>
    protected internal async Task<byte[]> DoOperationAsync(
        AfcOpCode opcode,
        byte[]? data = null,
        string? filename = null,
        CancellationToken cancellationToken = default
    ) {
        (AfcError status, byte[]? response) = await SendAndWaitAsync(opcode, data ?? [], cancellationToken: cancellationToken).ConfigureAwait(false);
        if (status == AfcError.Success) {
            return response;
        }

        string message = $"Opcode: {opcode} failed with status: {status}";
        if (filename is not null) {
            message += $" for file: {filename}";
        }

        throw status == AfcError.ObjectNotFound
            ? new AfcFileNotFoundException(message, status, filename)
            : new AfcException(message, status, filename);
    }

    /// <summary>
    /// Send one packet and wait for its response via the background reader. The write lock makes
    /// packet-number assignment, waiter registration and the send a single unit, so the reader always
    /// finds the waiter before the response arrives. Connects and starts the reader lazily.
    /// </summary>
    protected internal async Task<(AfcError Status, byte[] Data)> SendAndWaitAsync(
        AfcOpCode operation,
        ReadOnlyMemory<byte> data,
        ulong thisLength = 0,
        CancellationToken cancellationToken = default
    ) {
        await ConnectAsync(cancellationToken).ConfigureAwait(false);

        // Also restarts a reader that exited (e.g. connection error): the new one fails immediately on
        // the broken socket and propagates the error to this waiter instead of hanging forever.
        EnsureReader();

        TaskCompletionSource<(AfcError, byte[])> waiter = new TaskCompletionSource<(AfcError, byte[])>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            ulong packetNumber = _packetNumber;

            // register *before* sending
            _pending[packetNumber] = waiter;
            try {
                // Not cancellable: abandoning a half-written packet would corrupt the stream.
                await DispatchPacketAsync(operation, data, thisLength).ConfigureAwait(false);
            }
            catch {
                _pending.TryRemove(packetNumber, out _);
                throw;
            }
        }
        finally {
            _writeLock.Release();
        }

        return await waiter.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Shut down: stop the reader, fail every in-flight request with
    /// <see cref="ConnectionTerminatedException"/> and close the connection.
    /// </summary>
    public override async ValueTask CloseAsync() {
        Task? reader;
        lock (_readerLock) {
            reader = _readerTask;
            _readerCts?.Cancel();
            _readerTask = null;
        }

        // Closing the connection also unblocks a reader stuck in a read that ignores cancellation.
        await base.CloseAsync().ConfigureAwait(false);
        if (reader is not null) {
            try {
                await reader.ConfigureAwait(false);
            }
            catch (Exception) {
                // Reader shutdown errors are irrelevant once we're closing.
            }
        }

        FailAllPending(new ConnectionTerminatedException());
    }

    /// <summary>
    /// Yield <paramref name="root"/> and then every path below it, stopping descent once a directory's
    /// depth (count of <c>/</c>) reaches <paramref name="depth"/>. <c>-1</c> is unlimited; <c>0</c>
    /// yields only the root.
    /// </summary>
    public async IAsyncEnumerable<string> EnumerateDirectoryAsync(
        string root,
        int depth = -1,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    ) {
        await foreach ((string? folder, List<string>? dirs, List<string>? files) in WalkAsync(root, cancellationToken).ConfigureAwait(false)) {
            if (folder == root) {
                yield return folder;
                if (depth == 0) {
                    yield break;
                }
            }

            foreach (string? entry in dirs.Concat(files).ToList()) {
                yield return PosixPath.Join(folder, entry);
            }

            if (depth != -1) {
                // Prune in place so WalkAsync never reads directories beyond the requested depth.
                dirs.RemoveAll(d => PosixPath.Join(folder, d).Count(c => c == PosixPath.Separator) >= depth);
            }
        }
    }

    /// <summary>
    /// Whether a path exists. Implemented with <see cref="StatAsync"/>: a missing path is
    /// <c>false</c>; any other AFC error propagates.
    /// </summary>
    public async Task<bool> ExistsAsync(string filename, CancellationToken cancellationToken = default) {
        try {
            await StatAsync(filename, cancellationToken).ConfigureAwait(false);
        }
        catch (AfcFileNotFoundException) {
            return false;
        }

        return true;
    }

    /// <summary>Close a handle returned by <see cref="FOpenAsync"/>.</summary>
    public Task<byte[]> FileCloseAsync(
        ulong handle,
        CancellationToken cancellationToken = default
    ) => DoOperationAsync(
        AfcOpCode.FileRefClose,
        AfcProtocol.U64(handle),
        cancellationToken: cancellationToken
    );

    /// <summary>Open a file and return its handle.</summary>
    /// <param name="mode">One of <c>r</c>, <c>r+</c>, <c>w</c>, <c>w+</c>, <c>a</c>, <c>a+</c>.</param>
    /// <exception cref="ArgumentException">Unsupported mode.</exception>
    public async Task<ulong> FileOpenAsync(
        string filename,
        AfcFileOpenMode mode = AfcFileOpenMode.ReadOnly,
        CancellationToken cancellationToken = default
    ) {
        byte[] data = await DoOperationAsync(
            AfcOpCode.FileOpen,
            AfcProtocol.Concat(AfcProtocol.U64((ulong) mode), CString.GetBytes(filename)),
            cancellationToken: cancellationToken
        ).ConfigureAwait(false);
        if (data.Length < 8) {
            throw new AfcException("Truncated FILE_OPEN response", AfcError.InternalError, filename);
        }

        return BinaryPrimitives.ReadUInt64LittleEndian(data);
    }

    /// <summary>
    /// Read up to <paramref name="size"/> bytes from a handle. Reads over <see cref="MaximumReadSize"/>
    /// are split into successive operations.
    /// </summary>
    public async Task<byte[]> FileReadAsync(
        ulong handle,
        long size,
        CancellationToken cancellationToken = default
    ) {
        using (MemoryStream data = new MemoryStream()) {
            while (size > 0) {
                long toRead = size > MaximumReadSize ? MaximumReadSize : size;
                (AfcError status, byte[] chunk) = await SendAndWaitAsync(
                    AfcOpCode.FileRead,
                    AfcProtocol.Concat(AfcProtocol.U64(handle), AfcProtocol.U64((ulong) toRead)),
                    cancellationToken: cancellationToken
                ).ConfigureAwait(false);
                if (status != AfcError.Success) {
                    throw new AfcException("fread error", status);
                }

                size -= toRead;
                await data.WriteAsync(chunk, cancellationToken).ConfigureAwait(false);
            }

            return data.ToArray();
        }
    }

    /// <summary>Seek within a handle.</summary>
    public Task FileSeekAsync(
        ulong handle,
        long offset,
        SeekOrigin whence = SeekOrigin.Begin,
        CancellationToken cancellationToken = default
    ) => DoOperationAsync(
            AfcOpCode.FileSeek,
            AfcProtocol.Concat(AfcProtocol.U64(handle), AfcProtocol.U64((ulong) whence), AfcProtocol.I64(offset)),
            cancellationToken: cancellationToken
    );

    /// <summary>Write to a handle, split into <paramref name="chunkSize"/>-byte <c>WRITE</c> operations.</summary>
    public async Task FileWriteAsync(
        ulong handle,
        ReadOnlyMemory<byte> data,
        int chunkSize = MaximumWriteSize,
        CancellationToken cancellationToken = default
    ) {
        byte[] fileHandle = AfcProtocol.U64(handle);
        int chunksCount = data.Length / chunkSize;
        for (int i = 0; i < chunksCount; i++) {
            await WriteChunkAsync(
                fileHandle,
                data.Slice(i * chunkSize, chunkSize),
                "failed to write chunk",
                cancellationToken
            ).ConfigureAwait(false);
        }

        if (data.Length % chunkSize != 0) {
            await WriteChunkAsync(
                fileHandle,
                data[(chunksCount * chunkSize)..],
                "failed to write last chunk",
                cancellationToken
            ).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// File-system information (model, total/free bytes, block size…); all values are strings.
    /// </summary>
    public async Task<Dictionary<string, string>> GetDeviceInfoAsync(
        CancellationToken cancellationToken = default
    ) => AfcProtocol.ListToDict(
        await DoOperationAsync(AfcOpCode.GetDeviceInfo, cancellationToken: cancellationToken).ConfigureAwait(false)
    );

    /// <summary>Read a whole regular file (following a symbolic link).</summary>
    /// <exception cref="AfcException">The path is not a regular file or could not be opened.</exception>
    public async Task<byte[]> GetFileContentsAsync(
        string filename,
        CancellationToken cancellationToken = default
    ) {
        filename = await ResolvePathAsync(filename, cancellationToken).ConfigureAwait(false);
        AfcFileInfo info = await StatAsync(filename, cancellationToken).ConfigureAwait(false);

        if (!info.IsRegularFile) {
            throw new AfcException($"{filename} isn't a file", AfcError.InvalidArg, filename);
        }

        ulong h = await FileOpenAsync(filename, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (h == 0) {
            throw new AfcException($"failed to open {filename}", AfcError.InternalError, filename);
        }

        try {
            return await FileReadAsync(h, info.Size, cancellationToken).ConfigureAwait(false);
        }
        finally {
            await FileCloseAsync(h, CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <summary>Whether the path's <c>st_ifmt</c> is <c>S_IFDIR</c>.</summary>
    /// <exception cref="AfcFileNotFoundException">The path does not exist.</exception>
    public async Task<bool> IsDirAsync(
        string filename,
        CancellationToken cancellationToken = default
    ) => (await StatAsync(filename, cancellationToken).ConfigureAwait(false)).IsDirectory;


    /// <summary>
    /// Create a symbolic (default) or hard link at <paramref name="source"/> pointing to
    /// <paramref name="target"/>.
    /// </summary>
    public Task<byte[]> LinkAsync(
        string target,
        string source,
        AfcLinkType type = AfcLinkType.Symbolic,
        CancellationToken cancellationToken = default
    ) => DoOperationAsync(
        AfcOpCode.MakeLink,
        AfcProtocol.Concat(AfcProtocol.U64((ulong) type), CString.GetBytes(target), CString.GetBytes(source)),
        cancellationToken: cancellationToken
    );

    /// <summary>Directory entries, excluding <c>.</c> and <c>..</c>.</summary>
    /// <exception cref="AfcFileNotFoundException">The path does not exist.</exception>
    public async Task<List<string>> ListDirectoryAsync(
        string filename,
        CancellationToken cancellationToken = default
    ) {
        byte[] data = await DoOperationAsync(
            AfcOpCode.ReadDir,
            CString.GetBytes(filename),
            filename,
            cancellationToken
        ).ConfigureAwait(false);

        // Skip the . and ..
        return [.. AfcProtocol.ParseCStrings(data).Skip(2)];
    }

    /// <summary>Apply or release an advisory flock-style lock (<see cref="AfcLock"/>).</summary>
    public Task<byte[]> LockAsync(
        ulong handle,
        AfcLockModes operation,
        CancellationToken cancellationToken = default
    ) => DoOperationAsync(
        AfcOpCode.FileLock,
        AfcProtocol.Concat(AfcProtocol.U64(handle), AfcProtocol.U64((ulong) operation)),
        cancellationToken: cancellationToken
    );

    /// <summary>Create a directory, including any missing parents.</summary>
    public Task<byte[]> MakeDirsAsync(
        string filename,
        CancellationToken cancellationToken = default
    ) => DoOperationAsync(AfcOpCode.MakeDir, CString.GetBytes(filename), filename, cancellationToken);

    /// <summary>
    /// Pull (download) a file or directory from the device.
    /// </summary>
    /// <param name="relativeSrc">Device path, resolved relative to <paramref name="srcDir"/>.</param>
    /// <param name="dst">Local destination.</param>
    /// <param name="match">Only entries whose basename matches (anywhere — <c>Regex.IsMatch</c>) are copied.</param>
    /// <param name="callback">Called as <c>callback(src, dst)</c> after each regular file is copied.</param>
    /// <param name="srcDir">Base device directory joined with <paramref name="relativeSrc"/>.</param>
    /// <param name="ignoreErrors">Log and skip per-entry errors during directory recursion.</param>
    /// <param name="progressBar">Show a progress bar while streaming large files.</param>
    public async Task PullAsync(
        string relativeSrc,
        string dst,
        Regex? match = null,
        Func<string, string, ValueTask>? callback = null,
        string srcDir = "",
        bool ignoreErrors = false,
        bool progressBar = true,
        CancellationToken cancellationToken = default
    ) {
        string src = await ResolvePathAsync(PosixPath.Join(srcDir, relativeSrc), cancellationToken).ConfigureAwait(false);

        if (!await IsDirAsync(src, cancellationToken).ConfigureAwait(false)) {
            // normal file
            if (match is not null && !match.IsMatch(PosixPath.BaseName(src))) {
                return;
            }

            if (Directory.Exists(dst)) {
                dst = Path.Combine(dst, PosixPath.BaseName(relativeSrc));
            }

            await using (FileStream f = new FileStream(dst, FileMode.Create, FileAccess.Write, FileShare.None)) {
                long srcSize = (await StatAsync(src, cancellationToken).ConfigureAwait(false)).Size;
                if (srcSize <= MaximumReadSize) {
                    byte[] contents = await GetFileContentsAsync(src, cancellationToken).ConfigureAwait(false);
                    await f.WriteAsync(contents, cancellationToken).ConfigureAwait(false);
                }
                else {
                    ulong handle = await FileOpenAsync(src, cancellationToken: cancellationToken).ConfigureAwait(false);
                    try {
                        for (long offset = 0; offset < srcSize; offset += MaximumReadSize) {
                            int toRead = (int) Math.Min(MaximumReadSize, srcSize - offset);
                            byte[] chunk = await FileReadAsync(handle, toRead, cancellationToken).ConfigureAwait(false);
                            await f.WriteAsync(chunk, cancellationToken).ConfigureAwait(false);
                        }
                    }
                    finally {
                        await FileCloseAsync(handle, CancellationToken.None).ConfigureAwait(false);
                    }
                }
            }

            DateTimeOffset mtime = (await StatAsync(src, cancellationToken).ConfigureAwait(false)).MTime;
            File.SetLastWriteTimeUtc(dst, mtime.UtcDateTime);
            if (callback is not null) {
                await callback(src, dst).ConfigureAwait(false);
            }
        }
        else {
            // directory
            string dstPath = Path.Combine(dst, PosixPath.BaseName(relativeSrc));
            Directory.CreateDirectory(dstPath);

            foreach (string filename in await ListDirectoryAsync(src, cancellationToken).ConfigureAwait(false)) {
                string srcFilename = PosixPath.Join(src, filename);
                string dstFilename = Path.Combine(dstPath, filename);

                srcFilename = await ResolvePathAsync(srcFilename, cancellationToken).ConfigureAwait(false);

                if (match is not null && !match.IsMatch(PosixPath.BaseName(srcFilename))) {
                    continue;
                }

                try {
                    if (await IsDirAsync(srcFilename, cancellationToken).ConfigureAwait(false)) {
                        Directory.CreateDirectory(dstFilename);
                    }

                    await PullAsync(
                        srcFilename,
                        dstPath,
                        match,
                        callback,
                        ignoreErrors: ignoreErrors,
                        progressBar: progressBar,
                        cancellationToken: cancellationToken
                    ).ConfigureAwait(false);
                }
                catch (Exception e) when (ignoreErrors && e is not OperationCanceledException) {
                    Logger.LogWarning("(Ignoring) Error: {exception} occurred during the copy of {srcFilename}", e.Message, srcFilename);
                }
            }
        }
    }

    /// <summary>
    /// Push (upload) a file or directory to the device.
    /// </summary>
    /// <param name="callback">Called as <c>callback(localPath, remotePath)</c> before each entry is transferred.</param>
    public async Task PushAsync(
        string localPath,
        string remotePath,
        Func<string, string, ValueTask>? callback = null,
        CancellationToken cancellationToken = default
    ) {
        if (Directory.Exists(localPath)) {
            remotePath = PosixPath.Join(remotePath, Path.GetFileName(localPath));
        }
        await PushInternalAsync(
            localPath,
            remotePath,
            callback,
            cancellationToken
        ).ConfigureAwait(false);
    }

    /// <summary>
    /// Recursively remove a file or directory tree, depth-first, the directory itself last.
    /// </summary>
    /// <param name="force">Return the undeletable paths instead of throwing.</param>
    /// <returns>Paths that could not be deleted (empty on full success).</returns>
    public async Task<List<string>> RemoveAsync(
        string filename,
        Regex? match = null,
        bool force = false,
        CancellationToken cancellationToken = default
    ) {
        if (
            !await ExistsAsync(filename, cancellationToken).ConfigureAwait(false)
            && !await RemoveSingleAsync(filename, force, cancellationToken).ConfigureAwait(false)
        ) {
            return [filename];
        }

        // Single file
        if (!await IsDirAsync(filename, cancellationToken).ConfigureAwait(false)) {
            return await RemoveSingleAsync(filename, force, cancellationToken).ConfigureAwait(false) ? [] : [filename];
        }

        // Directory content
        List<string> undeletedItems = [];
        foreach (string entry in await ListDirectoryAsync(filename, cancellationToken).ConfigureAwait(false)) {
            string currentFilename = PosixPath.Join(filename, entry);
            if (match is not null && !MatchesAtStart(match, PosixPath.BaseName(currentFilename))) {
                continue;
            }

            if (await IsDirAsync(currentFilename, cancellationToken).ConfigureAwait(false)) {
                undeletedItems.AddRange(await RemoveAsync(currentFilename, force: true, cancellationToken: cancellationToken).ConfigureAwait(false));
            }
            else if (!await RemoveSingleAsync(currentFilename, force: true, cancellationToken).ConfigureAwait(false)) {
                undeletedItems.Add(currentFilename);
            }
        }

        // Directory path
        try {
            if (!await RemoveSingleAsync(filename, force, cancellationToken).ConfigureAwait(false)) {
                undeletedItems.Add(filename);
                return undeletedItems;
            }
        }
        catch (AfcException) when (undeletedItems.Count > 0) {
            undeletedItems.Add(filename);
        }

        if (undeletedItems.Count > 0) {
            throw new AfcException($"Failed to delete paths: [{string.Join(", ", undeletedItems.Select(p => $"'{p}'"))}]");
        }

        return [];
    }

    /// <summary>
    /// Remove a single file or empty directory (one <c>REMOVE_PATH</c>; does not recurse).
    /// </summary>
    /// <param name="force">Swallow an <see cref="AfcException"/> and return <c>false</c> instead of throwing.</param>
    /// <returns><c>true</c> on success; <c>false</c> if removal failed and <paramref name="force"/> is set.</returns>
    public async Task<bool> RemoveSingleAsync(
        string filename,
        bool force = false,
        CancellationToken cancellationToken = default
    ) {
        try {
            await DoOperationAsync(
                AfcOpCode.RemovePath,
                CString.GetBytes(filename),
                filename, cancellationToken
            ).ConfigureAwait(false);
        }
        catch (AfcException) when (force) {
            return false;
        }
        return true;
    }

    /// <summary>Rename or move a file or directory.</summary>
    /// <exception cref="AfcFileNotFoundException">The operation failed and <paramref name="source"/> no longer exists.</exception>
    /// <exception cref="AfcException">The operation failed while <paramref name="source"/> still exists.</exception>
    public async Task RenameAsync(
        string source,
        string target,
        CancellationToken cancellationToken = default
    ) {
        try {
            await DoOperationAsync(
                AfcOpCode.RenamePath,
                AfcProtocol.Concat(CString.GetBytes(source), CString.GetBytes(target)),
                cancellationToken: cancellationToken
            ).ConfigureAwait(false);
        }
        catch (AfcException e) {
            if (await ExistsAsync(source, cancellationToken).ConfigureAwait(false)) {
                throw;
            }
            throw new AfcFileNotFoundException($"Failed to rename {source} into {target}. Got status: {e.Status}", e.Status, source, e);
        }
    }

    /// <summary>
    /// If the path is a symbolic link, return its target (joined with the link's directory when
    /// relative); otherwise return it unchanged. Resolves a single level only.
    /// </summary>
    public async Task<string> ResolvePathAsync(string filename, CancellationToken cancellationToken = default) {
        AfcFileInfo info = await StatAsync(filename, cancellationToken).ConfigureAwait(false);
        if (info.IsSymlink) {
            string target = info["LinkTarget"];
            filename = target.StartsWith('/') ? target : PosixPath.Join(PosixPath.DirName(filename), target);
        }
        return filename;
    }

    /// <summary>Create or truncate a file and write <paramref name="data"/> to it.</summary>
    public async Task SetFileContentsAsync(
        string filename,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default
    ) {
        ulong h = await FileOpenAsync(filename, AfcFileOpenMode.WriteOnly, cancellationToken).ConfigureAwait(false);
        try {
            await FileWriteAsync(h, data, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        finally {
            await FileCloseAsync(h, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Connect and start the background reader.</summary>
    public async Task StartAsync(CancellationToken cancellationToken = default) {
        await ConnectAsync(cancellationToken).ConfigureAwait(false);
        EnsureReader();
    }

    /// <summary>
    /// File or directory metadata (<c>GET_FILE_INFO</c>). Symbolic links are not followed and carry a
    /// <see cref="AfcFileInfo.LinkTarget"/>.
    /// </summary>
    /// <exception cref="AfcFileNotFoundException">The path does not exist (AFC <c>OBJECT_NOT_FOUND</c> or <c>READ_ERROR</c>).</exception>
    public async Task<AfcFileInfo> StatAsync(string filename, CancellationToken cancellationToken = default) {
        byte[] data;
        try {
            data = await DoOperationAsync(
                AfcOpCode.GetFileInfo,
                CString.GetBytes(filename),
                filename,
                cancellationToken
            ).ConfigureAwait(false);
        }
        catch (AfcException e) when (e.Status == AfcError.ReadError) {
            throw new AfcFileNotFoundException(e.Message, e.Status, filename, e);
        }

        return new AfcFileInfo(AfcProtocol.ListToDict(data));
    }


    /// <summary>Poll <see cref="ExistsAsync"/> every 0.1 s until the path appears.</summary>
    public async Task WaitExistsAsync(
        string filename,
        CancellationToken cancellationToken = default
    ) {
        while (!await ExistsAsync(filename, cancellationToken).ConfigureAwait(false)) {
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Walk a tree top-down. Symbolic links to directories count as files.
    /// Remove names from <see cref="AfcWalkEntry.DirNames"/> before continuing to prune the descent.
    /// </summary>
    public async IAsyncEnumerable<AfcWalkEntry> WalkAsync(
        string dirname,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    ) {
        List<string> dirs = [];
        List<string> files = [];

        foreach (string fd in await ListDirectoryAsync(dirname, cancellationToken).ConfigureAwait(false)) {
            if (fd is "." or ".." or "") {
                continue;
            }

            AfcFileInfo info = await StatAsync(PosixPath.Join(dirname, fd), cancellationToken).ConfigureAwait(false);
            (info.IsDirectory ? dirs : files).Add(fd);
        }

        yield return new AfcWalkEntry(dirname, dirs, files);

        for (int i = 0; i < dirs.Count; i++) {
            await foreach (AfcWalkEntry? item in WalkAsync(PosixPath.Join(dirname, dirs[i]), cancellationToken).ConfigureAwait(false)) {
                yield return item;
            }
        }
    }

    /// <summary>
    /// Wait until the AFC connection terminates (e.g. the device disconnects). Makes sure the reader is
    /// running so termination is noticed even with no operation in flight; returns immediately if the
    /// connection already died.
    /// </summary>
    public async Task WaitTerminatedAsync(CancellationToken cancellationToken = default) {
        if (!_terminated.Task.IsCompleted) {
            await ConnectAsync(cancellationToken).ConfigureAwait(false);
            EnsureReader();
        }
        await _terminated.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }
}

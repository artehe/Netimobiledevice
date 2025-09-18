using Microsoft.Extensions.Logging;
using Netimobiledevice.Afc.Packets;
using Netimobiledevice.EndianBitConversion;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Plist;
using Netimobiledevice.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Afc;

/// <summary>
/// Interact with the publicly available directories and files
/// </summary>
/// <param name="lockdown"></param>
/// <param name="serviceName"></param>
/// <param name="logger"></param>
public class AfcService(LockdownServiceProvider lockdown, string serviceName = "", ILogger? logger = null) : LockdownService(lockdown, GetServiceName(lockdown, serviceName), logger: logger)
{
    private const string LOCKDOWN_SERVICE_NAME = "com.apple.afc";
    private const string RSD_SERVICE_NAME = "com.apple.afc.shim.remote";

    private const int MAXIMUM_READ_SIZE = 1024 * 1024; // 1 MB

    private static string[] DirectoryTraversalFiles { get; } = [".", "..", ""];

    private ulong _packetNumber;

    private async Task DispatchPacket(AfcOpCode opCode, AfcPacket packet, CancellationToken cancellationToken, ulong? thisLength = null)
    {
        packet.Header = new AfcHeader() {
            EntireLength = (ulong) packet.PacketSize,
            Length = (ulong) packet.PacketSize,
            PacketNumber = _packetNumber,
            Operation = opCode
        };
        if (thisLength != null) {
            packet.Header.Length = (ulong) thisLength;
        }

        _packetNumber++;
        await Service.SendAsync(packet.GetBytes(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads data from an open file handle and writes it to the specified local file path.
    /// </summary>
    /// <param name="handle">The handle of the remote file to read from.</param>
    /// <param name="size">The total size of the file in bytes.</param>
    /// <param name="downloadFilePath">The local file path where the file will be saved.</param>
    /// <param name = "progressTracker" >A callback function that reports download progress, providing the total number of bytes downloaded.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests during the download process.</param>
    /// <returns>A task representing the asynchronous file download operation.</returns>
    /// <exception cref="AfcException">
    /// Thrown when an AFC protocol error occurs or if the file read operation fails.
    /// </exception>
    private async Task DownloadFileAsync(ulong handle, ulong size, string downloadFilePath, IProgress<long>? progressTracker, CancellationToken cancellationToken)
    {
        int totalBytes = 0;
        using FileStream? fileStream = new FileStream(downloadFilePath, FileMode.Create);
        AfcFileReadRequest packet = new AfcFileReadRequest {
            Handle = handle,
            Size = size
        };

        while (size > (ulong) totalBytes) {
            await DispatchPacket(AfcOpCode.FileRefRead, packet, cancellationToken).ConfigureAwait(false);

            byte[] fileChunkBytes = await Service.ReceiveAsync(AfcHeader.GetSize(), cancellationToken).ConfigureAwait(false);
            AfcError afcError = AfcError.Success;
            byte[] fileChunk = [];
            if (fileChunkBytes.Length != 0) {
                AfcHeader afcHeader = AfcHeader.FromBytes(fileChunkBytes);
                if (afcHeader.EntireLength < (ulong) AfcHeader.GetSize()) {
                    throw new AfcException("Expected more bytes in afc header than receieved");
                }

                int num = (int) afcHeader.EntireLength - AfcHeader.GetSize();
                fileChunk = await Service.ReceiveAsync(num, cancellationToken);
                if (afcHeader.Operation == AfcOpCode.Status) {
                    if (num != 8) {
                        Logger?.LogWarning("Status length is not 8 bytes long");
                    }
                    afcError = (AfcError) BitConverter.ToUInt64(fileChunk, 0);
                }
            }

            if (afcError != AfcError.Success) {
                throw new AfcException(afcError, "File Read Error");
            }

            int bytesRead = fileChunk.Length;

            await fileStream.WriteAsync(fileChunk.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);

            totalBytes += bytesRead;
            progressTracker?.Report(totalBytes);
        }
    }

    private static string GetServiceName(LockdownServiceProvider lockdown, string serviceName)
    {
        if (string.IsNullOrEmpty(serviceName)) {
            if (lockdown is LockdownClient) {
                return LOCKDOWN_SERVICE_NAME;
            }
            else {
                return RSD_SERVICE_NAME;
            }
        }
        return serviceName;
    }

    private static List<string> ParseFileInfoResponseForMessage(byte[] data)
    {
        string decodedData = Encoding.UTF8.GetString(data);
        List<string> seperatedData = [.. decodedData.Split('\0')];
        seperatedData.RemoveAt(seperatedData.Count - 1);
        return seperatedData;
    }

    private static Dictionary<string, string> ParseFileInfoResponseToDict(byte[] data)
    {
        Dictionary<string, string> result = [];

        string decodedData = Encoding.UTF8.GetString(data);
        List<string> seperatedData = [.. decodedData.Split('\0')];

        seperatedData.RemoveAt(seperatedData.Count - 1);
        if (seperatedData.Count % 2 != 0) {
            throw new AfcException("Received data not balanced, unable to parse to dictionary");
        }

        for (int i = 0; i < seperatedData.Count; i += 2) {
            result[seperatedData[i]] = seperatedData[i + 1];
        }
        return result;
    }

    private async Task<(AfcError, byte[])> ReceiveData(CancellationToken cancellationToken)
    {
        byte[] response = await Service.ReceiveAsync(AfcHeader.GetSize(), cancellationToken).ConfigureAwait(false);

        AfcError status = AfcError.Success;
        byte[] data = [];

        if (response.Length > 0) {
            AfcHeader header = AfcHeader.FromBytes(response);
            if (header.EntireLength < (ulong) AfcHeader.GetSize()) {
                throw new AfcException("Expected more bytes in afc header than receieved");
            }
            int length = (int) header.EntireLength - AfcHeader.GetSize();
            data = Service.Receive(length);
            if (header.Operation == AfcOpCode.Status) {
                if (length != 8) {
                    Logger?.LogWarning("Status length is not 8 bytes long");
                }
                ulong statusValue = BitConverter.ToUInt64(data, 0);
                status = (AfcError) statusValue;
            }
        }

        return (status, data);
    }

    private async Task<string> ResolvePath(string filename, CancellationToken cancellationToken)
    {
        DictionaryNode info = await GetFileInfo(filename, cancellationToken).ConfigureAwait(false) ?? [];
        if (info.TryGetValue("st_ifmt", out PropertyNode? stIfmt) && stIfmt.AsStringNode().Value == "S_IFLNK") {
            string target = info["LinkTarget"].AsStringNode().Value;
            if (!target.StartsWith('/')) {
                // Relative path
                string filePath = Path.GetDirectoryName(filename) ?? string.Empty;
                filename = Path.Combine(filePath, target);
            }
            else {
                filename = target;
            }
        }
        return filename;
    }

    /// <summary>
    /// return true if succeess or raise an exception depending on force parameter.
    /// </summary>
    /// <param name="filename">path to directory or a file</param>
    /// <param name="force">True for ignore exception and return False</param>
    /// <returns></returns>
    private async Task<bool> RmSingle(string filename, CancellationToken cancellationToken, bool force = false)
    {
        AfcRmRequest request = new AfcRmRequest(filename);
        try {
            await RunOperation(AfcOpCode.RemovePath, request, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (AfcException) {
            if (force) {
                return false;
            }
            else {
                throw;
            }
        }
    }

    private async Task<byte[]> RunOperation(AfcOpCode opCode, AfcPacket packet, CancellationToken cancellationToken)
    {
        await DispatchPacket(opCode, packet, cancellationToken).ConfigureAwait(false);
        (AfcError status, byte[] recievedData) = await ReceiveData(cancellationToken).ConfigureAwait(false);
        if (status != AfcError.Success) {
            if (status == AfcError.ObjectNotFound) {
                throw new AfcFileNotFoundException(AfcError.ObjectNotFound);
            }
            throw new AfcException($"Afc opcode {opCode} failed with status: {status}");
        }
        return recievedData;
    }

    /// <summary>
    /// Downloads the contents of a file from the specified source path to a local destination.
    /// </summary>
    /// <param name="sourceFilePath">The remote source file path to download from.</param>
    /// <param name="downloadFilePath">The local file path where the downloaded file will be saved.</param>
    /// <param name="progressTracker">A callback function to report download progress (source file and total bytes downloaded).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous download operation.</returns>
    /// <exception cref="AfcException">
    /// Thrown when the file info cannot be retrieved or the source path does not point to a regular file.
    /// </exception>
    public async Task DownloadFileContentsAsync(string sourceFilePath, string downloadFilePath, IProgress<long>? progressTracker, CancellationToken cancellationToken)
    {
        sourceFilePath = await ResolvePath(sourceFilePath, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        DictionaryNode info = (await GetFileInfo(sourceFilePath, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)) ?? [];
        if (!info.TryGetValue("st_ifmt", out PropertyNode? value)) {
            throw new AfcException(AfcError.ObjectNotFound, "couldn't find st_ifmt in file info");
        }

        if (value.AsStringNode().Value != "S_IFREG") {
            throw new AfcException(AfcError.InvalidArg, sourceFilePath + " isn't a file");
        }

        ulong handle = await FileOpen(sourceFilePath, cancellationToken, AfcFileOpenMode.ReadOnly).ConfigureAwait(continueOnCapturedContext: false);
        if (handle == 0L) {
            return;
        }

        await DownloadFileAsync(handle, info["st_size"].AsIntegerNode().Value, downloadFilePath, progressTracker, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        await FileClose(handle, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
    }

    public async Task<bool> Exists(string filename, CancellationToken cancellationToken)
    {
        try {
            await GetFileInfo(filename, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (AfcException ex) {
            if (ex.AfcError == AfcError.ObjectNotFound) {
                return false;
            }
            else {
                throw;
            }
        }
    }

    public async Task FileClose(ulong handle, CancellationToken cancellationToken)
    {
        AfcFileCloseRequest request = new AfcFileCloseRequest(handle);
        await RunOperation(AfcOpCode.FileRefClose, request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ulong> FileOpen(string filename, CancellationToken cancellationToken, AfcFileOpenMode mode = AfcFileOpenMode.ReadOnly)
    {
        AfcFileOpenRequest openRequest = new AfcFileOpenRequest(mode, filename);
        byte[] data = await RunOperation(AfcOpCode.FileRefOpen, openRequest, cancellationToken).ConfigureAwait(false);
        return AfcFileOpenResponse.FromBytes(data).Handle;
    }

    public async Task<byte[]> FileRead(ulong handle, ulong size, CancellationToken cancellationToken = default)
    {
        byte[] result = new byte[size];
        int offset = 0;
        while (size > 0) {
            ulong toRead = Math.Min(size, MAXIMUM_READ_SIZE);

            AfcFileReadRequest readRequest = new AfcFileReadRequest() {
                Handle = handle,
                Size = (ulong) toRead
            };
            await DispatchPacket(AfcOpCode.FileRefRead, readRequest, cancellationToken).ConfigureAwait(false);

            (AfcError status, byte[] chunk) = await ReceiveData(cancellationToken).ConfigureAwait(false);
            if (status != AfcError.Success) {
                throw new AfcException(status, "File Read Error");
            }

            int bytesRead = chunk.Length;
            if ((ulong) bytesRead < toRead) {
                throw new AfcException(AfcError.NotEnoughData, $"Expected {toRead} and got {bytesRead} bytes");
            }

            Buffer.BlockCopy(chunk, 0, result, offset, chunk.Length);

            offset += bytesRead;
            size -= (ulong) bytesRead;
        }
        return result;
    }

    /// <summary>
    /// Seeks to a given position of a pre-opened file on the device.
    /// </summary>
    /// <param name="handle">File handle of a previously opened.</param>
    /// <param name="offset">Seek offset.</param>
    /// <param name="whence">Seeking direction, one of SEEK_SET, SEEK_CUR, or SEEK_END.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="AfcException"></exception>
    public async Task FileSeek(ulong handle, long offset, ulong whence, CancellationToken cancellationToken = default)
    {
        if (handle == 0) {
            throw new AfcException(AfcError.InvalidArg);
        }

        // Send the command
        AfcSeekInfoRequest seekInfo = new AfcSeekInfoRequest(handle, whence, offset);
        await DispatchPacket(AfcOpCode.FileRefSeek, seekInfo, cancellationToken).ConfigureAwait(false);

        // Receive response
        (AfcError status, byte[] _) = await ReceiveData(cancellationToken);
        if (status != AfcError.Success) {
            throw new AfcException(status);
        }
    }

    /// <summary>
    /// Returns current position in a pre-opened file on the device.
    /// </summary>
    /// <param name="handle">File handle of a previously opened.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Position in bytes of indicator</returns>
    public async Task<ulong> FileTell(ulong handle, CancellationToken cancellationToken = default)
    {
        if (handle == 0) {
            throw new AfcException(AfcError.InvalidArg);
        }

        // Send the command 
        AfcTellRequest packet = new AfcTellRequest(handle);
        await DispatchPacket(AfcOpCode.FileRefTell, packet, cancellationToken).ConfigureAwait(false);

        // Receive the data 
        (AfcError status, byte[] data) = await ReceiveData(cancellationToken).ConfigureAwait(false);
        if (data.Length > 0) {
            // Get the position 
            ulong value = EndianBitConverter.LittleEndian.ToUInt64(data, 0);
            return value;
        }
        throw new AfcException(status);
    }

    public async Task FileWrite(ulong handle, byte[] data, CancellationToken cancellationToken, int chunkSize = 4096)
    {
        ulong dataSize = (ulong) data.Length;
        int chunksCount = data.Length / chunkSize;
        Logger?.LogDebug("Writing {dataSize} bytes in {chunksCount} chunks", dataSize, chunksCount);

        List<byte> writtenData = [];
        for (int i = 0; i < chunksCount; i++) {
            cancellationToken.ThrowIfCancellationRequested();
            Logger?.LogDebug("Writing chunk {i}", i);

            AfcFileWritePacket packet = new AfcFileWritePacket(handle, [.. data.Skip(i * chunkSize).Take(chunkSize)]);
            await DispatchPacket(AfcOpCode.FileRefWrite, packet, cancellationToken, 48).ConfigureAwait(false);
            writtenData.AddRange(packet.Data);

            (AfcError status, byte[] _) = await ReceiveData(cancellationToken).ConfigureAwait(false);
            if (status != AfcError.Success) {
                throw new AfcException(status, $"Failed to write chunk: {status}");
            }
            Logger?.LogDebug("Chunk {i} written", i);
        }

        if (dataSize % (ulong) chunkSize > 0) {
            Logger?.LogDebug("Writing last chunk");
            AfcFileWritePacket packet = new AfcFileWritePacket(handle, [.. data.Skip(chunksCount * chunkSize)]);
            await DispatchPacket(AfcOpCode.FileRefWrite, packet, cancellationToken, 48).ConfigureAwait(false);
            writtenData.AddRange(packet.Data);

            (AfcError status, byte[] _) = await ReceiveData(cancellationToken).ConfigureAwait(false);
            if (status != AfcError.Success) {
                throw new AfcException(status, $"Failed to write last chunk: {status}");
            }
            Logger?.LogDebug("Last chunk written");
        }
    }

    public async Task<List<string>> GetDirectoryList(CancellationToken cancellationToken)
    {
        List<string> directoryList = [];
        try {
            AfcFileInfoRequest request = new AfcFileInfoRequest("/");
            byte[] response = await RunOperation(AfcOpCode.ReadDir, request, cancellationToken).ConfigureAwait(false);
            directoryList = ParseFileInfoResponseForMessage(response);
        }
        catch (Exception ex) {
            Logger?.LogError(ex, "Error trying to get directory list");
        }
        return directoryList;
    }

    public async Task<List<string>> GetDirectoryList(string directory, CancellationToken cancellationToken)
    {
        AfcFileInfoRequest packet = new AfcFileInfoRequest(directory);
        return ParseFileInfoResponseForMessage(await RunOperation(AfcOpCode.ReadDir, packet, cancellationToken).ConfigureAwait(continueOnCapturedContext: false));

    }

    public async Task<byte[]?> GetFileContents(string filename, CancellationToken cancellationToken)
    {
        filename = await ResolvePath(filename, cancellationToken).ConfigureAwait(false);

        DictionaryNode info = await GetFileInfo(filename, cancellationToken).ConfigureAwait(false) ?? [];
        if (!info.TryGetValue("st_ifmt", out PropertyNode? stIfmtNode)) {
            throw new AfcException(AfcError.ObjectNotFound, "couldn't find st_ifmt in file info");
        }

        if (stIfmtNode.AsStringNode().Value != "S_IFREG") {
            throw new AfcException(AfcError.InvalidArg, $"{filename} isn't a file");
        }

        ulong handle = await FileOpen(filename, cancellationToken).ConfigureAwait(false);
        if (handle == 0) {
            return null;
        }
        byte[] details = await FileRead(handle, info["st_size"].AsIntegerNode().Value, cancellationToken).ConfigureAwait(false);

        await FileClose(handle, cancellationToken).ConfigureAwait(false);
        return details;
    }

    public async Task<DictionaryNode?> GetFileInfo(string filename, CancellationToken cancellationToken)
    {
        Dictionary<string, string> stat;
        try {
            AfcFileInfoRequest request = new AfcFileInfoRequest(filename);
            byte[] response = await RunOperation(AfcOpCode.GetFileInfo, request, cancellationToken).ConfigureAwait(false);
            stat = ParseFileInfoResponseToDict(response);
        }
        catch (AfcException ex) {
            if (ex.AfcError != AfcError.ReadError) {
                throw;
            }
            throw new AfcFileNotFoundException(ex.AfcError, filename);
        }

        if (stat.Count == 0) {
            return null;
        }

        // Convert timestamps from unix epoch ticks (nanoseconds) to DateTime
        long divisor = (long) Math.Pow(10, 6);
        long mTimeMilliseconds = long.Parse(stat["st_mtime"], CultureInfo.InvariantCulture.NumberFormat) / divisor;
        long birthTimeMilliseconds = long.Parse(stat["st_birthtime"], CultureInfo.InvariantCulture.NumberFormat) / divisor;

        DateTime mTime = DateTimeOffset.FromUnixTimeMilliseconds(mTimeMilliseconds).LocalDateTime;
        DateTime birthTime = DateTimeOffset.FromUnixTimeMilliseconds(birthTimeMilliseconds).LocalDateTime;

        DictionaryNode fileInfo = new DictionaryNode {
            { "st_ifmt", new StringNode(stat["st_ifmt"]) },
            { "st_size", new IntegerNode(ulong.Parse(stat["st_size"], CultureInfo.InvariantCulture.NumberFormat)) },
            { "st_blocks", new IntegerNode(ulong.Parse(stat["st_blocks"], CultureInfo.InvariantCulture.NumberFormat)) },
            { "st_nlink", new IntegerNode(ulong.Parse(stat["st_nlink"], CultureInfo.InvariantCulture.NumberFormat)) },
            { "st_mtime", new DateNode(mTime) },
            { "st_birthtime", new DateNode(birthTime) }
        };

        return fileInfo;
    }

    public async Task<bool> IsDir(string filename, CancellationToken cancellationToken)
    {
        DictionaryNode stat = await GetFileInfo(filename, cancellationToken).ConfigureAwait(false) ?? [];
        if (stat.TryGetValue("st_ifmt", out PropertyNode? value)) {
            return value.AsStringNode().Value == "S_IFDIR";
        }
        return false;
    }

    private async Task<List<string>> ListDirectory(string filename, CancellationToken cancellationToken)
    {
        byte[] data = await RunOperation(AfcOpCode.ReadDir, new AfcReadDirectoryRequest(filename), cancellationToken);
        // Make sure to skip "." and ".."
        return [.. AfcReadDirectoryResponse.Parse(data).Filenames.Skip(2)];
    }

    public async Task<byte[]> Lock(ulong handle, AfcLockModes operation, CancellationToken cancellationToken)
    {
        AfcLockRequest request = new AfcLockRequest(handle, (ulong) operation);
        return await RunOperation(AfcOpCode.FileRefLock, request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// List the files and folders in the given directory
    /// </summary>
    /// <param name="path">Path to list</param>
    /// <param name="depth">Listing depth, -1 to list infinite depth</param>
    /// <returns>List of files found</returns>
    public async IAsyncEnumerable<string> LsDirectory(string path, [EnumeratorCancellation] CancellationToken cancellationToken, int depth = -1)
    {
        await foreach ((string folder, List<string> dirs, List<string> files) in Walk(path, cancellationToken).ConfigureAwait(false)) {
            if (folder == path) {
                yield return folder;
                if (depth == 0) {
                    break;
                }
            }
            if (folder != path && depth != -1 && folder.Count(x => x == Path.DirectorySeparatorChar) >= depth) {
                continue;
            }

            List<string> results = [.. dirs.ToArray(), .. files.ToArray()];
            foreach (string entry in results) {
                yield return $"{folder}/{entry}";
            }
        }
    }

    public async Task Pull(string relativeSrc, string dst, CancellationToken cancellationToken, string srcDir = "")
    {
        string src = srcDir;
        if (string.IsNullOrEmpty(src)) {
            src = relativeSrc;
        }
        else {
            src = $"{src}/{relativeSrc}";
        }

        string[] splitSrc = relativeSrc.Split('/');
        string dstPath = splitSrc.Length > 1 ? Path.Combine(dst, splitSrc[^1]) : Path.Combine(dst, relativeSrc);
        if (OperatingSystem.IsWindows()) {
            // Windows filesystems (NTFS) are more restrictive than unix files systems so we gotta sanitise
            dstPath = PathSanitiser.SantiseWindowsPath(dstPath);
        }
        Logger?.LogInformation("{src} --> {dst}", src, dst);

        src = await ResolvePath(src, cancellationToken).ConfigureAwait(false);
        if (!await IsDir(src, cancellationToken).ConfigureAwait(false)) {
            // Normal file
            using (FileStream fs = new FileStream(dstPath, FileMode.Create)) {
                byte[]? data = await GetFileContents(src, cancellationToken).ConfigureAwait(false);
                await fs.WriteAsync(data, cancellationToken).ConfigureAwait(false);
            }
        }
        else {
            // Directory
            Directory.CreateDirectory(dstPath);
            foreach (string filename in await ListDirectory(src, cancellationToken).ConfigureAwait(false)) {
                string dstFilename = Path.Combine(dstPath, filename);
                string srcFilename = await ResolvePath($"{src}/{filename}", cancellationToken).ConfigureAwait(false);

                if (await IsDir(srcFilename, cancellationToken).ConfigureAwait(false)) {
                    Directory.CreateDirectory(dstFilename);
                }
                await Pull(srcFilename, dstPath, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Recursive removal of a directory or a file, if any did not succeed then return list of undeleted filenames or raise exception depending on force parameter.
    /// </summary>
    /// <param name="filename">path to directory or a file</param>
    /// <param name="force">True for ignore exception and return list of undeleted paths</param>
    /// <returns>A list of undeleted paths</returns>
    public async Task<List<string>> Rm(string filename, CancellationToken cancellationToken, bool force = false)
    {
        if (!await Exists(filename, cancellationToken).ConfigureAwait(false)) {
            if (!await RmSingle(filename, cancellationToken, force: force).ConfigureAwait(false)) {
                return [filename];
            }
        }

        // Single file
        if (!await IsDir(filename, cancellationToken).ConfigureAwait(false)) {
            if (await RmSingle(filename, cancellationToken, force: force).ConfigureAwait(false)) {
                return [];
            }
            return [filename];
        }

        // Directory Content
        List<string> undeletedItems = [];
        foreach (string entry in await ListDirectory(filename, cancellationToken).ConfigureAwait(false)) {
            string currentFile = $"{filename}/{entry}";
            if (await IsDir(currentFile, cancellationToken).ConfigureAwait(false)) {
                List<string> retUndeletedItems = await Rm(currentFile, cancellationToken, force: true).ConfigureAwait(false);
                undeletedItems.AddRange(retUndeletedItems);
            }
            else {
                if (!await RmSingle(currentFile, cancellationToken, force: true).ConfigureAwait(false)) {
                    undeletedItems.Add(currentFile);
                }
            }

        }

        // Directory Path
        try {
            if (!await RmSingle(filename, cancellationToken, force: force).ConfigureAwait(false)) {
                undeletedItems.Add(filename);
                return undeletedItems;
            }
        }
        catch (AfcException) {
            if (undeletedItems.Count > 0) {
                undeletedItems.Add(filename);
            }
            else {
                throw;
            }
        }

        if (undeletedItems.Count > 0) {
            throw new AfcException($"Failed to delete paths: {string.Join(", ", undeletedItems)}");
        }

        return [];
    }

    public async Task SetFileContents(string filename, byte[] data, CancellationToken cancellationToken)
    {
        ulong handle = await FileOpen(filename, cancellationToken, AfcFileOpenMode.WriteOnly).ConfigureAwait(false);
        if (handle == 0) {
            throw new AfcException(AfcError.OpenFailed, "Failed to open file for writing.");
        }
        await FileWrite(handle, data, cancellationToken).ConfigureAwait(false);
        await FileClose(handle, cancellationToken).ConfigureAwait(false);
    }

    private async IAsyncEnumerable<Tuple<string, List<string>, List<string>>> Walk(string directory, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        List<string> directories = [];
        List<string> files = [];

        foreach (string fd in await ListDirectory(directory, cancellationToken).ConfigureAwait(false)) {
            if (DirectoryTraversalFiles.Contains(fd)) {
                continue;
            }

            DictionaryNode fileInfo = await GetFileInfo($"{directory}/{fd}", cancellationToken).ConfigureAwait(false) ?? [];
            if (fileInfo.TryGetValue("st_ifmt", out PropertyNode? value)) {
                if (value is StringNode node && node.Value == "S_IFDIR") {
                    directories.Add(fd);
                }
                else {
                    files.Add(fd);
                }
            }
        }
        yield return Tuple.Create(directory, directories, files);

        foreach (string dir in directories) {
            await foreach (Tuple<string, List<string>, List<string>> result in Walk($"{directory}/{dir}", cancellationToken).ConfigureAwait(false)) {
                yield return result;
            }
        }
    }
}

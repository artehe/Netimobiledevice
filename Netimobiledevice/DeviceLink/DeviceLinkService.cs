using Microsoft.Extensions.Logging;
using Netimobiledevice.Backup;
using Netimobiledevice.EndianBitConversion;
using Netimobiledevice.Exceptions;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Plist;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.DeviceLink
{
    public delegate void SendFileErrorEventHandler(DictionaryNode errorNode, string fileName);

    internal sealed class DeviceLinkService : IDisposable
    {
        private const int BULK_OPERATION_ERROR = -13;
        private const UInt32 FILE_TRANSFER_TERMINATOR = 0x00;
        // Set the default timeout to be 5 minutes
        private const int SERVICE_TIMEOUT = 5 * 60 * 1000;

        private readonly ServiceConnection _service;
        private readonly string _rootPath;
        private readonly ILogger _logger;
        private CancellationTokenSource internalCancellationTokenSource;
        
        private FileStream? _fileStream;
        private BackupFile _lastBackupFile = new();
        
        public long BytesRead { get; protected set; }
        
        private void CloseFileStream() {
            try {
                _fileStream?.Flush();
            } catch (Exception fex) {
                _logger.LogError($"Error flushing backup file : {fex.Message}");
            }
            _fileStream?.Close();
            _fileStream = null;
        }
        
        private long _ticksSpentReceiving = 0;
        private long _ticksTotalReceive = 0;
        private long _ticksSpentWritingFiles = 0;


        private void CaptureTotalTicks(long startTicks) {
            _ticksTotalReceive += DateTime.Now.Ticks - startTicks;
        }

        public void LogPerformanceData() {
            _logger.LogCritical(new EventId(1, "NetworkReceiveDuration"), $"{TimeSpan.FromTicks(_ticksSpentReceiving)}");
            _logger.LogCritical(new EventId(1, "FileWriteDuration"), $"{TimeSpan.FromTicks(_ticksSpentWritingFiles)}");
            _logger.LogCritical(new EventId(1, "CombinedReceiveWriteDuration"), $"{TimeSpan.FromTicks(_ticksSpentWritingFiles + _ticksSpentReceiving)}");
            _logger.LogCritical(new EventId(1, "ReceiveFileDuration"), $"{TimeSpan.FromTicks(_ticksTotalReceive)}");
        }

        private Dictionary<string, Func<ArrayNode, CancellationToken, Task>> DeviceLinkHandlers { get; }
        /// <summary>
        /// A list of the files whose transfer failed due to a device error.
        /// </summary>
        private List<BackupFile> FailedFiles { get; } = [];

        /// <summary>
        /// Event raised when a file is about to be transferred from the device.
        /// </summary>
        public event EventHandler<BackupFileEventArgs>? BeforeReceivingFile;
        /// <summary>
        /// Event raised when the backup finishes.
        /// </summary>
        public event EventHandler<BackupResultEventArgs>? Completed;
        /// <summary>
        /// Event raised when there is some error during the backup.
        /// </summary>
        public event EventHandler<DetailedErrorEventArgs>? Error;
        /// <summary>
        /// Event raised when there is a non-fatal error during the backup
        /// </summary>
        public event EventHandler<DetailedErrorEventArgs>? Warning;
        /// <summary>
        /// Event raised when a file is received from the device.
        /// </summary>
        public event EventHandler<BackupFileEventArgs>? FileReceived;
        /// <summary>
        /// Event raised when a part of a file has been received from the device.
        /// </summary>
        public event EventHandler<BackupFileEventArgs>? FileReceiving;
        /// <summary>
        /// Event raised when a file transfer has failed due an internal device error.
        /// </summary>
        public event EventHandler<BackupFileErrorEventArgs>? FileTransferError;
        /// <summary>
        /// Event raised when the device requires a passcode to start the backup
        /// </summary>
        public event EventHandler? PasscodeRequiredForBackup;
        /// <summary>
        /// Event raised for signaling the backup progress.
        /// </summary>
        public event ProgressChangedEventHandler? Progress;
        /// <summary>
        /// Event raised when the backup started.
        /// </summary>
        public event EventHandler? Started;
        /// <summary>
        /// Event raised for signaling different kinds of the backup status.
        /// </summary>
        public event EventHandler<StatusEventArgs>? Status;
        /// <summary>
        /// Event raised when a file send to the device has failed due to errors on the application side
        /// </summary>
        public event SendFileErrorEventHandler? SendFileError;
        

        public DeviceLinkService(ServiceConnection service, string backupDirectory, ILogger logger)
        {
            _service = service;
            _rootPath = backupDirectory;
            _logger = logger;

            internalCancellationTokenSource = new CancellationTokenSource();

            // Adjust the timeout to be long enough to handle device with a large amount of data
            _service.SetTimeout(SERVICE_TIMEOUT);

            DeviceLinkHandlers = new Dictionary<string, Func<ArrayNode, CancellationToken, Task>>() {
                { DeviceLinkMessage.ContentsOfDirectory, ContentsOfDirectory },
                { DeviceLinkMessage.CopyItem, CopyItem },
                { DeviceLinkMessage.CreateDirectory, CreateDirectory },
                { DeviceLinkMessage.Disconnect, DisconnectAsync },
                { DeviceLinkMessage.DownloadFiles, DownloadFiles },
                { DeviceLinkMessage.GetFreeDiskSpace, GetFreeDiskSpace },
                { DeviceLinkMessage.MoveFiles, MoveItems },
                { DeviceLinkMessage.MoveItems, MoveItems },
                { DeviceLinkMessage.PurgeDiskSpace, PurgeDiskSpace },
                { DeviceLinkMessage.RemoveFiles, RemoveItems },
                { DeviceLinkMessage.RemoveItems, RemoveItems },
                { DeviceLinkMessage.UploadFiles, UploadFiles }
            };
        }

        /// <summary>
        /// Manages the ListDirectory device message.
        /// </summary>
        /// <param name="msg">The message received from the device.</param>
        /// <returns>Always 0.</returns>
        private async Task ContentsOfDirectory(ArrayNode msg, CancellationToken cancellationToken)
        {
            string path = Path.Combine(_rootPath, msg[1].AsStringNode().Value);
            DictionaryNode dirList = [];
            DirectoryInfo dir = new DirectoryInfo(path);
            if (dir.Exists) {
                foreach (FileSystemInfo entry in dir.GetFileSystemInfos()) {
                    if (cancellationToken.IsCancellationRequested) {
                        break;
                    }
                    DictionaryNode entryDict = new DictionaryNode {
                        { "DLFileModificationDate", new DateNode(entry.LastWriteTime) },
                        { "DLFileSize", new IntegerNode(entry is FileInfo fileInfo ? fileInfo.Length : 0L) },
                        { "DLFileType", new StringNode(entry.Attributes.HasFlag(FileAttributes.Directory) ? "DLFileTypeDirectory" : "DLFileTypeRegular") }
                    };
                    dirList.Add(entry.Name, entryDict);
                }
            }

            if (!cancellationToken.IsCancellationRequested) {
                await SendStatusReport(0, null, dirList, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Manages the CopyItem device message.
        /// </summary>
        /// <param name="msg">The message received from the device.</param>
        /// <returns>The errno result of the operation.</returns>
        private async Task CopyItem(ArrayNode msg, CancellationToken cancellationToken)
        {
            FileInfo source = new FileInfo(Path.Combine(_rootPath, msg[1].AsStringNode().Value));
            FileInfo dest = new FileInfo(Path.Combine(_rootPath, msg[2].AsStringNode().Value));
            if (source.Attributes.HasFlag(FileAttributes.Directory)) {
                _logger.LogError("Trying to coppy a whole directory rather than an individual file");
            }
            else {
                source.CopyTo(dest.FullName);
            }
            await SendStatusReport(0, string.Empty, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Manages the CreateDirectory device message.
        /// </summary>
        /// <param name="msg">The message received from the device.</param>
        /// <returns>The errno result of the operation.</returns>
        private async Task CreateDirectory(ArrayNode msg, CancellationToken cancellationToken)
        {
            string newDirPath = Path.Combine(_rootPath, msg[1].AsStringNode().Value);
            Directory.CreateDirectory(newDirPath);
            await SendStatusReport(0, string.Empty, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a dictionary plist instance of the required error report for the device.
        /// </summary>
        /// <param name="errorNo">The errno code.</param>
        private static DictionaryNode CreateErrorReport(ErrNo errorNo)
        {
            string errMsg;
            int errCode = -(int)errorNo;

            if (errorNo == ErrNo.ENOENT) {
                errCode = -6;
                errMsg = "No such file or directory.";
            }
            else if (errorNo == ErrNo.EEXIST) {
                errCode = -7;
                errMsg = "File or directory already exists.";
            }
            else {
                errMsg = $"Unspecified error: ({errorNo})";
            }

            DictionaryNode dict = new DictionaryNode() {
                { "DLFileErrorString", new StringNode(errMsg) },
                { "DLFileErrorCode", new IntegerNode(errCode) }
            };
            return dict;
        }

        private void Disconnect()
        {
            Task.Run(async () => await DisconnectAsync([], CancellationToken.None).ConfigureAwait(false)).GetAwaiter().GetResult();
        }

        private async Task DisconnectAsync(ArrayNode msg, CancellationToken cancellationToken)
        {
            ArrayNode message = [
                new StringNode("DLMessageDisconnect"),
                new StringNode("___EmptyParameterString___")
            ];
            try {
                await _service.SendPlistAsync(message, PlistFormat.Binary, cancellationToken).ConfigureAwait(false);
            }
            catch (ObjectDisposedException) {
                _logger.LogWarning("Trying to send disconnect from disposed service");
            }
        }

        /// <summary>
        /// Manages the DownloadFiles device message.
        /// </summary>
        /// <param name="msg">The message received from the device.</param>
        private async Task DownloadFiles(ArrayNode msg, CancellationToken cancellationToken)
        {
            DictionaryNode errList = [];
            ArrayNode files = msg[1].AsArrayNode();
            foreach (StringNode filename in files.Cast<StringNode>()) {
                _logger.LogDebug("Sending file: {filename}", filename);
                if (cancellationToken.IsCancellationRequested) {
                    break;
                }
                await SendPath(filename.Value, cancellationToken).ConfigureAwait(false);

                string filePath = Path.Combine(_rootPath, filename.Value);

                var errorCode = ErrNo.ENOERR;
                if (!File.Exists(filePath)) {
                    errorCode = ErrNo.ENOENT;
                }

                await using (FileStream fs = File.OpenRead(filePath)) {
                    // We want to use a chunk size of 128 MiB
                    byte[] chunk = new byte[128 * 1024 * 1024];

                    int bytesRead;
                    while ((bytesRead = await fs.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0) {
                        List<byte> data = [
                            (byte) ResultCode.FileData,
                            .. chunk.Take(bytesRead)
                        ];
                        await SendPrefixed([.. data], data.Count, cancellationToken).ConfigureAwait(false);
                    }
                }

                if (errorCode == ErrNo.ENOERR) {
                    byte[] buffer = [(byte) ResultCode.Success];
                    await SendPrefixed(buffer, buffer.Length, cancellationToken).ConfigureAwait(false);
                }
                else {
                    _logger.LogDebug("Sending Error Code: {code}", errorCode);
                    DictionaryNode errReport = CreateErrorReport(errorCode);
                    errList.Add(filename.Value, errReport);
                    await SendError(errReport, cancellationToken).ConfigureAwait(false);
                    OnSendFileError(errReport, filename.Value);
                }
            }

            await _service.SendAsync(BitConverter.GetBytes(FILE_TRANSFER_TERMINATOR), cancellationToken).ConfigureAwait(false);
            if (errList.Count == 0) {
                await SendStatusReport(0, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            else {
                await SendStatusReport(BULK_OPERATION_ERROR, "Multi status", errList, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Manages the GetFreeDiskSpace device message.
        /// </summary>
        /// <param name="msg">The message received from the device.</param>
        /// <param name="respectFreeSpaceValue">Whether the device should abide by the freeSpace value passed or ignore it</param>
        /// <returns>0 on success, -1 on error.</returns>
        private async Task GetFreeDiskSpace(ArrayNode msg, CancellationToken cancellationToken)
        {
            long freeSpace = 0;
            DirectoryInfo dir = new DirectoryInfo(_rootPath);
            foreach (DriveInfo drive in DriveInfo.GetDrives()) {
                try {
                    if (drive.IsReady && drive.Name == dir.Root.FullName) {
                        freeSpace = drive.AvailableFreeSpace;
                        break;
                    }
                }
                catch (Exception ex) {
                    _logger.LogError(ex, $"Issue getting space from drive: {ex}");
                    Warning?.Invoke(this, new DetailedErrorEventArgs(ex, _rootPath));
                    
                }
            }

            IntegerNode spaceItem = new IntegerNode(freeSpace);
            await SendStatusReport(0, null, spaceItem, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Manages the MoveItems device message.
        /// </summary>
        /// <param name="msg">The message received from the device.</param>
        /// <returns>The number of items moved.</returns>
        private async Task MoveItems(ArrayNode msg, CancellationToken cancellationToken)
        {
            foreach (KeyValuePair<string, PropertyNode> move in msg[1].AsDictionaryNode()) {
                if (cancellationToken.IsCancellationRequested) {
                    break;
                }

                string newPath = move.Value.AsStringNode().Value;
                if (!string.IsNullOrEmpty(newPath)) {
                    FileInfo newFile = new FileInfo(Path.Combine(_rootPath, newPath));
                    if (newFile.Exists) {
                        if (newFile.Attributes.HasFlag(FileAttributes.Directory)) {
                            new DirectoryInfo(newFile.FullName).Delete(true);
                        }
                        else {
                            newFile.Delete();
                        }
                    }

                    FileInfo oldFile = new FileInfo(Path.Combine(_rootPath, move.Key));
                    if (oldFile.Exists) {
                        oldFile.MoveTo(newFile.FullName);
                    }
                }
            }

            if (!cancellationToken.IsCancellationRequested) {
                await SendStatusReport(0, string.Empty, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }

        private void OnSendFileError(DictionaryNode errorReport, string fileName) {
            SendFileError?.Invoke(errorReport, fileName);
        }

        /// <summary>
        /// Event handler called after a file has been received from the device.
        /// </summary>
        /// <param name="file">The file received.</param>
        private void OnFileReceived(BackupFile file)
        {
            if (_lastBackupFile == file && _fileStream != null) {
                try {
                    CloseFileStream();
                } catch (Exception ex) {
                    BackupFileErrorEventArgs e = new BackupFileErrorEventArgs(file, $"{ex.Message} : {ex.StackTrace}");
                    FileTransferError?.Invoke(this, e);
                }
            }
            
            FileReceived?.Invoke(this, new BackupFileEventArgs(file));
            
            if (string.Equals("Status.plist", Path.GetFileName(file.LocalPath), StringComparison.OrdinalIgnoreCase)) {
                try {
                    using (FileStream fs = File.OpenRead(file.LocalPath)) {
                        DictionaryNode statusPlist = PropertyList.Load(fs).AsDictionaryNode();
                        OnStatusReceived(new BackupStatus(statusPlist, _logger));
                    }
                } catch (Exception ex) {
                    BackupFileErrorEventArgs e = new BackupFileErrorEventArgs(file, $"{ex.Message} : {ex.StackTrace}");
                    FileTransferError?.Invoke(this, e);
                }
            }
        }

        /// <summary>
        /// Event handler called after a part (or all of) a file has been sent from the device from the device.
        /// </summary>
        /// <param name="file">The file received.</param>
        /// <param name="fileData">The file contents received</param>
        private void OnFileReceiving(BackupFile file, byte[] fileData)
        {
            // Ensure the directory requested exists before writing to it.
            string? pathDir = Path.GetDirectoryName(file.LocalPath);
            if (!string.IsNullOrWhiteSpace(pathDir) && !Directory.Exists(file.LocalPath)) {
                Directory.CreateDirectory(pathDir);
            }

            using (FileStream stream = File.OpenWrite(file.LocalPath)) {
                stream.Seek(0, SeekOrigin.End);
                stream.Write(fileData, 0, fileData.Length);
            }

            FileReceiving?.Invoke(this, new BackupFileEventArgs(file, fileData));
        }

        /// <summary>
        /// Event handler called after a file transfer failed due to a device error.
        /// </summary>
        /// <param name="file">The file whose tranfer failed.</param>
        private void OnFileTransferError(BackupFile file, string details)
        {
            CloseFileStream();
            FailedFiles.Add(file);
            if (FileTransferError != null) {
                BackupFileErrorEventArgs e = new BackupFileErrorEventArgs(file, details);
                FileTransferError.Invoke(this, e);
                internalCancellationTokenSource.Cancel();
            }
        }

        /// <summary>
        /// Event handler called each time the backup service sends a status report.
        /// </summary>
        /// <param name="status">The status report sent from the backup service.</param>
        private void OnStatusReceived(BackupStatus status)
        {
            string snapshotState = $"{status.SnapshotState}";
            Status?.Invoke(this, new StatusEventArgs(snapshotState, status));
            _logger.LogDebug("OnStatus: {message}", snapshotState);
        }

        private async Task PurgeDiskSpace(ArrayNode message, CancellationToken cancellationToken)
        {
            throw new DeviceLinkException("Not enough Disk space for operation");
        }

        private async Task<ResultCode> ReadCode(CancellationToken cancellationToken)
        {
            byte[] buffer = await _service.ReceiveAsync(1, cancellationToken).ConfigureAwait(false);
            byte code = buffer[0];
            if (!Enum.IsDefined(typeof(ResultCode), code)) {
                _logger.LogWarning("New backup code found: {code}", code);
            }
            return (ResultCode) code;
        }

        /// <summary>
        /// Reads an Int32 value from the backup service.
        /// </summary>
        /// <returns>The Int32 value read.</returns>
        private async Task<int> ReadInt32(CancellationToken cancellationToken)
        {
            byte[] buffer = await _service.ReceiveAsync(4, cancellationToken).ConfigureAwait(false);
            if (buffer.Length > 0) {
                return EndianBitConverter.BigEndian.ToInt32(buffer, 0);
            }
            return -1;
        }

        /// <summary>
        /// Reads the information of the next file that the backup service will send.
        /// </summary>
        /// <returns>Returns the file information of the next file to download, or null if there are no more files to download.</returns>
        private async Task<BackupFile?> ReceiveBackupFile(CancellationToken cancellationToken)
        {
            string devicePath = await ReceiveFilename(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrEmpty(devicePath)) {
                return null;
            }
            string backupPath = await ReceiveFilename(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrEmpty(backupPath)) {
                _logger.LogWarning("Error reading backup file path.");
            }
            return new BackupFile(devicePath, backupPath, _rootPath);
        }

        /// <summary>
        /// Receives a single file from the device.
        /// </summary>
        /// <param name="file">The BackupFile to receive.</param>
        /// <param name="skip">Indicates whether to skip or save the file.</param>
        /// <returns>The result code of the transfer.</returns>
        private async Task<ResultCode> ReceiveFile(BackupFile file, CancellationToken cancellationToken)
        {
            var startTicks = DateTime.UtcNow.Ticks;
            
            const int bufferLen = 32 * 1024;
            ResultCode lastCode = ResultCode.Success;
            if (File.Exists(file.LocalPath)) {
                File.Delete(file.LocalPath);
            }
            while (!cancellationToken.IsCancellationRequested) {
                // Size is the number of bytes left to read
                int size = await ReadInt32(cancellationToken).ConfigureAwait(false);
                if (size <= 0) {
                    break;
                }

                ResultCode code = await ReadCode(cancellationToken).ConfigureAwait(false);
                int blockSize = size - sizeof(ResultCode);
                if (code != ResultCode.FileData) {
                    if (code == ResultCode.Success) {
                        CloseFileStream();
                        CaptureTotalTicks(startTicks);
                        return code;
                    }

                    string msg = string.Empty;
                    if (blockSize > 0) {
                        byte[] msgBuffer = await _service.ReceiveAsync(blockSize, cancellationToken).ConfigureAwait(false);
                        msg = Encoding.UTF8.GetString(msgBuffer);
                    }

                    // iOS 17 beta devices seem to give RemoteError for a fair number of file now?
                    _logger.LogWarning("Failed to fully upload {localPath}. Device file name {devicePath}. Reason: {msg}", file.LocalPath, file.DevicePath, msg);

                    OnFileTransferError(file, $"{code}: {msg} [ExpectedSize: {file.ExpectedFileSize}, ActualReceived: {file.FileSize} ]");
                    CaptureTotalTicks(startTicks);
                    _logger.LogDebug($"Time: ReceiveFileTotalTicks {TimeSpan.FromTicks(_ticksTotalReceive)}");
                    return code;
                }
                lastCode = code;

                int done = 0;
                while (done < blockSize) {
                    int toRead = Math.Min(blockSize - done, bufferLen);
                    byte[] buffer = await _service.ReceiveAsync(toRead, cancellationToken).ConfigureAwait(false);

                    OnFileReceiving(file, buffer);
                    
                    done += buffer.Length;
                    
                    BytesRead += buffer.Length; // Track the total bytes read this session
                }

                if (done == blockSize) {
                    file.FileSize += blockSize;
                }
                
                _logger.LogDebug($"Time: NetworkRecv: {TimeSpan.FromTicks(_ticksSpentReceiving)} FileWrite: {TimeSpan.FromTicks(_ticksSpentWritingFiles)} Combined: {TimeSpan.FromTicks(_ticksSpentWritingFiles + _ticksSpentReceiving)}");
            }
            
            CaptureTotalTicks(startTicks);

            return lastCode;
        }

        /// <summary>
        /// Reads a filename from the backup service stream.
        /// </summary>>
        /// <returns>The filename read from the backup stream, or NULL if there are no more files.</returns>
        private async Task<string> ReceiveFilename(CancellationToken cancellationToken)
        {
            int len = await ReadInt32(cancellationToken).ConfigureAwait(false);
            if (len == 0) {
                // A zero length means no more files to receive.
                return string.Empty;
            }
            byte[] buffer = await _service.ReceiveAsync(len, cancellationToken).ConfigureAwait(false);
            return Encoding.UTF8.GetString(buffer);
        }

        /// <summary>
        /// Manages the RemoveItems device message.
        /// </summary>
        /// <param name="msg">The message received from the device.</param>
        /// <returns>The number of items removed.</returns>
        private async Task RemoveItems(ArrayNode message, CancellationToken cancellationToken)
        {
            ArrayNode removes = message[1].AsArrayNode();
            foreach (StringNode filename in removes.Cast<StringNode>()) {
                if (cancellationToken.IsCancellationRequested) {
                    break;
                }

                if (string.IsNullOrEmpty(filename.Value)) {
                    _logger.LogWarning("Empty file to remove.");
                }
                else {
                    FileInfo file = new FileInfo(Path.Combine(_rootPath, filename.Value));
                    if (file.Exists) {
                        if (file.Attributes.HasFlag(FileAttributes.Directory)) {
                            Directory.Delete(file.FullName, true);
                        }
                        else {
                            file.Delete();
                        }
                    }
                }
            }

            if (!cancellationToken.IsCancellationRequested) {
                await SendStatusReport(0, string.Empty, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }


        /// <summary>
        /// Sends the specified error report to the backup service.
        /// </summary>
        /// <param name="error">The error report to send.</param>
        public async Task SendError(DictionaryNode errorReport, CancellationToken cancellationToken)
        {
            byte[] errBytes = Encoding.UTF8.GetBytes(errorReport["DLFileErrorString"].AsStringNode().Value);
            List<byte> buffer = [
                (byte) ResultCode.LocalError, .. errBytes
            ];
            await SendPrefixed([.. buffer], buffer.Count, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends a filename to the backup service stream.
        /// </summary>
        /// <param name="filename">The filename to send.</param>
        private async Task SendPath(string filename, CancellationToken cancellationToken)
        {
            byte[] path = Encoding.UTF8.GetBytes(filename);
            await SendPrefixed(path, path.Length, cancellationToken).ConfigureAwait(false);
        }

        private async Task SendPrefixed(byte[] data, int length, CancellationToken cancellationToken)
        {
            await _service.SendAsync(EndianBitConverter.BigEndian.GetBytes(length), cancellationToken).ConfigureAwait(false);
            await _service.SendAsync(data, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends a status report to the backup service.
        /// </summary>
        /// <param name="errorCode">The error code to send (as errno value).</param>
        /// <param name="errorMessage">The error message to send.</param>
        /// <param name="errorList">A PropertyNode with additional value(s).</param>
        private async Task SendStatusReport(int errorCode, string? errorMessage = null, PropertyNode? errorList = null, CancellationToken cancellationToken = default)
        {
            ArrayNode array = [
                new StringNode("DLMessageStatusResponse"),
                new IntegerNode(errorCode),
                errorMessage != null ? new StringNode(errorMessage) : new StringNode("___EmptyParameterString___"),
                errorList ?? new DictionaryNode(),
            ];

            await _service.SendPlistAsync(array, PlistFormat.Binary, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Updates the progress as signaled by the backup service message.
        /// </summary>
        /// <param name="msg">The message received containing the progress information.</param>
        /// <param name="index">The index of the element in the array that contains the progress value.</param>
        private void UpdateProgressForMessage(RealNode progressNode)
        {
            if (progressNode.Value > 0.0) {
                Progress?.Invoke(this, new ProgressChangedEventArgs((int) progressNode.Value, BytesRead));
            }
        }

        /// <summary>
        /// Manages the UploadFiles device message.
        /// </summary>
        /// <param name="msg">The message received from the device.</param>
        /// <returns>The number of files processed.</returns>
        private async Task UploadFiles(ArrayNode msg, CancellationToken cancellationToken)
        {
            string errorDescription = string.Empty;
            int fileCount = 0;
            int errorCode = 0;

            long backupTotalSize = (long) msg[3].AsIntegerNode().Value;
            if (backupTotalSize > 0) {
                _logger.LogDebug("Backup total size: {backupTotalSize}", backupTotalSize);
            }

            while (!cancellationToken.IsCancellationRequested) {
                BackupFile? backupFile = await ReceiveBackupFile(cancellationToken).ConfigureAwait(false);
                if (backupFile != null) {
                    backupFile.ExpectedFileSize = backupTotalSize;
                    _logger.LogDebug("Receiving file {BackupPath}", backupFile.BackupPath);
                    BeforeReceivingFile?.Invoke(this, new BackupFileEventArgs(backupFile, backupTotalSize));
                    ResultCode code = await ReceiveFile(backupFile, cancellationToken).ConfigureAwait(false);
                    if (code == ResultCode.Success) {
                        OnFileReceived(backupFile);
                    }
                    fileCount++;
                }
                else if (_service.IsConnected) {
                    break;
                }
                else {
                    throw new DeviceDisconnectedException();
                }
            }

            if (!cancellationToken.IsCancellationRequested) {
                await SendStatusReport(errorCode, errorDescription, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }


        public void Dispose()
        {
            CloseFileStream();
            Disconnect();
            _service.Close();
            GC.SuppressFinalize(this);
        }

        public async Task<ResultCode> DlLoop(CancellationToken cancellationToken)
        {
            FailedFiles.Clear();

            internalCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            while (!cancellationToken.IsCancellationRequested) {
                ArrayNode message = await ReceiveMessage(internalCancellationTokenSource.Token).ConfigureAwait(false);

                string command = message[0].AsStringNode().Value;
                _logger.LogDebug("Command recieved: {command}", command);
                if (command == DeviceLinkMessage.ProcessMessage) {
                    if (message[1].AsDictionaryNode()["ErrorCode"].AsIntegerNode().Value != (ulong) ResultCode.Success) {
                        throw new DeviceLinkException($"Device link error: {PropertyList.SaveAsString(message[1], PlistFormat.Xml)}");
                    }
                    Completed?.Invoke(this, new BackupResultEventArgs(FailedFiles, false, false));
                    return ResultCode.Success;
                }
                else if (command == DeviceLinkMessage.GetFreeDiskSpace) {
                    // We don't do anything specific for this command we just don't want to update progress as there isn't any attached to this message.
                }
                else if (command == DeviceLinkMessage.UploadFiles) {
                    UpdateProgressForMessage(message[2].AsRealNode());
                }
                else {
                    UpdateProgressForMessage(message[3].AsRealNode());
                }

                await DeviceLinkHandlers[command](message, internalCancellationTokenSource.Token).ConfigureAwait(false);
            }
            return ResultCode.Skipped;
        }

        public async Task<ArrayNode> ReceiveMessage(CancellationToken cancellationToken)
        {
            PropertyNode? message = await _service.ReceivePlistAsync(cancellationToken).ConfigureAwait(false);
            if (message == null) {
                return [];
            }
            return message.AsArrayNode();
        }

        public void SendProcessMessage(PropertyNode message)
        {
            _service.SendPlist(new ArrayNode() {
                new StringNode("DLMessageProcessMessage"),
                message
            }, PlistFormat.Binary);
        }

        /// <summary>
        /// Performs the DLMessageVersionExchange with the connected device. 
        /// This should be the first operation to be executed by an implemented
        /// device link service client.
        /// </summary>
        /// <param name="versionMajor">The major version number to check.</param>
        /// <param name="versionMinor">The minor version number to check.</param>
        public async Task VersionExchange(ulong versionMajor, ulong versionMinor, CancellationToken cancellationToken)
        {
            // Get DLMessageVersionExchange from device
            ArrayNode versionExchangeMessage = await ReceiveMessage(cancellationToken);
            string dlMessage = versionExchangeMessage[0].AsStringNode().Value;
            if (string.IsNullOrEmpty(dlMessage) || dlMessage != "DLMessageVersionExchange") {
                throw new DeviceLinkException("Didn't receive DLMessageVersionExchange from device");
            }
            if (versionExchangeMessage.Count < 3) {
                throw new DeviceLinkException("DLMessageVersionExchange has unexpected format");
            }

            // Get major and minor version number
            ulong vMajor = versionExchangeMessage[1].AsIntegerNode().Value;
            ulong vMinor = versionExchangeMessage[2].AsIntegerNode().Value;
            if (vMajor > versionMajor) {
                throw new DeviceLinkException($"Version mismatch detected received {vMajor}.{vMinor}, expected {versionMajor}.{versionMinor}");
            }
            else if (vMajor == versionMajor && vMinor > versionMinor) {
                throw new DeviceLinkException($"Version mismatch detected received {vMajor}.{vMinor}, expected {versionMajor}.{versionMinor}");
            }

            // The version is ok so send reply
            _service.SendPlist(new ArrayNode {
                new StringNode("DLMessageVersionExchange"),
                new StringNode("DLVersionsOk"),
                new IntegerNode(versionMajor)
            }, PlistFormat.Binary);

            // Receive DeviceReady message
            ArrayNode messageDeviceReady = await ReceiveMessage(cancellationToken);
            dlMessage = messageDeviceReady[0].AsStringNode().Value;
            if (string.IsNullOrEmpty(dlMessage) || dlMessage != "DLMessageDeviceReady") {
                throw new DeviceLinkException("Device link didn't return ready state (DLMessageDeviceReady)");
            }
        }
    }
}

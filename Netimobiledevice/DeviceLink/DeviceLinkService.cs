using Microsoft.Extensions.Logging;
using Netimobiledevice.Backup;
using Netimobiledevice.EndianBitConversion;
using Netimobiledevice.Exceptions;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Lockdown.Services;
using Netimobiledevice.Plist;
using Netimobiledevice.Usbmuxd;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.DeviceLink
{
    public abstract class DeviceLinkService : BaseService
    {
        // Set the default timeout to be 5 minutes
        private const int SERVICE_TIMEOUT = 5 * 60 * 1000;

        private const int BULK_OPERATION_ERROR = -13;
        private const UInt32 FILE_TRANSFER_TERMINATOR = 0x00;

        /// <summary>
        /// Event raised when a file is received from the device.
        /// </summary>
        public event EventHandler<DLFileEventArgs>? DLFileReceived;
        /// <summary>
        /// Event raised for signaling the backup progress.
        /// </summary>
        public event ProgressChangedEventHandler? DLProgress;
        /// <summary>
        /// Event raised when a file transfer has failed due an internal device error.
        /// </summary>
        public event EventHandler<DLFileEventArgs>? DLFileTransferError;
        /// <summary>
        /// Event raised for signaling different kinds of the backup status.
        /// </summary>
        public event EventHandler<StatusEventArgs>? DLStatus;

        protected DeviceLinkService(LockdownClient lockdown, ServiceConnection? service = null) : base(lockdown, service)
        {
            // Adjust the timeout to be long enough to handle device with a large amount of data
            Service.SetTimeout(SERVICE_TIMEOUT);
        }

        /// <summary>
        /// Creates a dictionary plist instance of the required error report for the device.
        /// </summary>
        /// <param name="errorNo">The errno code.</param>
        private static DictionaryNode CreateErrorReport(int errorNo)
        {
            string errMsg;
            int errCode = -errorNo;

            if (errorNo == (int) ErrNo.ENOENT) {
                errCode = -6;
                errMsg = "No such file or directory.";
            }
            else if (errorNo == (int) ErrNo.EEXIST) {
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
            ArrayNode message = new ArrayNode {
                new StringNode("DLMessageDisconnect"),
                new StringNode("___EmptyParameterString___")
            };
            try {
                Service.SendPlist(message, PlistFormat.Binary);
            }
            catch (ObjectDisposedException) {
                Logger.LogWarning("Trying to send disconnect from disposed service");
            }
        }

        /// <summary>
        /// Event handler called each time the backup service sends a status report.
        /// </summary>
        /// <param name="status">The status report sent from the backup service.</param>
        private void OnStatusReceived(DictionaryNode status)
        {
            CultureInfo cultureInfo = CultureInfo.InvariantCulture;
            TextInfo textInfo = cultureInfo.TextInfo;
            string snapshotStateString = textInfo.ToTitleCase(status["SnapshotState"].AsStringNode().Value);
            if (Enum.TryParse(snapshotStateString, out SnapshotState snapshotState)) {
                DLStatus?.Invoke(this, new StatusEventArgs($"{snapshotState}"));
            }
        }

        private void UpdateProgressForMessage(ArrayNode msg, int index)
        {
            DLProgress?.Invoke(this, new ProgressChangedEventArgs((int) msg[index].AsRealNode().Value, null));
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
            await Service.SendAsync(EndianBitConverter.BigEndian.GetBytes(length), cancellationToken).ConfigureAwait(false);
            await Service.SendAsync(data, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Manages the CopyItem device message.
        /// </summary>
        /// <param name="msg">The message received from the device.</param>
        /// <returns>The errno result of the operation.</returns>
        private DLResultCode OnCopyItem(ArrayNode msg, string rootPath)
        {
            string srcPath = Path.Combine(rootPath, msg[1].AsStringNode().Value);
            string dstPath = Path.Combine(rootPath, msg[2].AsStringNode().Value);

            FileInfo source = new FileInfo(srcPath);
            if (source.Attributes.HasFlag(FileAttributes.Directory)) {
                Logger.LogError("Trying to cppy a whole directory rather than an individual file");
            }
            else {
                File.Copy(source.FullName, new FileInfo(dstPath).FullName);
            }
            SendStatusReport(0, string.Empty);
            return DLResultCode.MessageComplete;
        }


        /// <summary>
        /// Manages the CreateDirectory device message.
        /// </summary>
        /// <param name="msg">The message received from the device.</param>
        /// <returns>The errno result of the operation.</returns>
        private DLResultCode OnCreateDirectory(ArrayNode msg, string rootPath)
        {
            UpdateProgressForMessage(msg, 3);
            string newDirPath = Path.Combine(rootPath, msg[1].AsStringNode().Value);
            Directory.CreateDirectory(newDirPath);
            SendStatusReport(0, string.Empty);
            return DLResultCode.MessageComplete;
        }

        /// <summary>
        /// Manages the DownloadFiles device message.
        /// </summary>
        /// <param name="msg">The message received from the device.</param>
        private async Task<DLResultCode> OnDownloadFiles(ArrayNode msg, string rootPath, CancellationToken cancellationToken = default)
        {
            UpdateProgressForMessage(msg, 3);

            DictionaryNode errList = new DictionaryNode();
            ArrayNode files = msg[1].AsArrayNode();
            foreach (StringNode filename in files.Cast<StringNode>()) {
                Logger.LogDebug("Sending file: {filename}", filename);
                if (cancellationToken.IsCancellationRequested) {
                    break;
                }
                await SendPath(filename.Value, cancellationToken).ConfigureAwait(false);

                string filePath = Path.Combine(rootPath, filename.Value);

                int errorCode = 0;
                if (!File.Exists(filePath)) {
                    errorCode = 2;
                }

                using (FileStream fs = File.OpenRead(filePath)) {
                    // We want to use a chunk size of 128 MiB
                    byte[] chunk = new byte[128 * 1024 * 1024];

                    int bytesRead;
                    while ((bytesRead = await fs.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0) {
                        List<byte> data = new List<byte> {
                            (byte) DLResultCode.FileData
                        };
                        data.AddRange(chunk.Take(bytesRead));
                        await SendPrefixed(data.ToArray(), data.Count, cancellationToken).ConfigureAwait(false);
                    }
                }

                if (errorCode == 0) {
                    byte[] buffer = new byte[] { (byte) DLResultCode.Success };
                    await SendPrefixed(buffer, buffer.Length, cancellationToken).ConfigureAwait(false);
                }
                else {
                    Logger.LogDebug("Sending Error Code: {code}", errorCode);
                    DictionaryNode errReport = CreateErrorReport(errorCode);
                    errList.Add(filename.Value, errReport);
                    await SendError(errReport, cancellationToken).ConfigureAwait(false);
                }
            }

            await Service.SendAsync(BitConverter.GetBytes(FILE_TRANSFER_TERMINATOR), cancellationToken).ConfigureAwait(false);
            if (errList.Count == 0) {
                SendStatusReport(0);
            }
            else {
                SendStatusReport(BULK_OPERATION_ERROR, "Multi status", errList);
            }
            return DLResultCode.MessageComplete;
        }

        /// <summary>
        /// Manages the GetFreeDiskSpace device message.
        /// </summary>
        /// <param name="msg">The message received from the device.</param>
        /// <param name="respectFreeSpaceValue">Whether the device should abide by the freeSpace value passed or ignore it</param>
        /// <returns>0 on success, -1 on error.</returns>
        private DLResultCode OnGetFreeDiskSpace(string rootPath, bool respectFreeSpaceValue = true)
        {
            long freeSpace = 0;
            DirectoryInfo dir = new DirectoryInfo(rootPath);
            foreach (DriveInfo drive in DriveInfo.GetDrives()) {
                try {
                    if (drive.IsReady && drive.Name == dir.Root.FullName) {
                        freeSpace = drive.AvailableFreeSpace;
                        break;
                    }
                }
                catch (Exception ex) {
                    Logger.LogError(ex, "Issue getting space from drive");
                }
            }

            IntegerNode spaceItem = new IntegerNode(freeSpace);
            if (respectFreeSpaceValue) {
                SendStatusReport(0, null, spaceItem);
            }
            else {
                SendStatusReport(-1, null, spaceItem);
            }
            return DLResultCode.MessageComplete;
        }

        /// <summary>
        /// Manages the ListDirectory device message.
        /// </summary>
        /// <param name="msg">The message received from the device.</param>
        /// <returns>Always 0.</returns>
        private DLResultCode OnListDirectory(ArrayNode msg, string rootPath, CancellationToken cancellationToken)
        {
            string path = Path.Combine(rootPath, msg[1].AsStringNode().Value);
            DictionaryNode dirList = new DictionaryNode();
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
            SendStatusReport(0, null, dirList);
            return DLResultCode.MessageComplete;
        }

        /// <summary>
        /// Manages the MoveItems device message.
        /// </summary>
        /// <param name="msg">The message received from the device.</param>
        /// <returns>The number of items moved.</returns>
        private DLResultCode OnMoveItems(ArrayNode msg, string rootPath, CancellationToken cancellationToken)
        {
            UpdateProgressForMessage(msg, 3);
            int res = 0;
            foreach (KeyValuePair<string, PropertyNode> move in msg[1].AsDictionaryNode()) {
                if (cancellationToken.IsCancellationRequested) {
                    break;
                }

                string newPath = move.Value.AsStringNode().Value;
                if (!string.IsNullOrEmpty(newPath)) {
                    res++;
                    FileInfo newFile = new FileInfo(Path.Combine(rootPath, newPath));
                    FileInfo oldFile = new FileInfo(Path.Combine(rootPath, move.Key));

                    if (newFile.Exists) {
                        if (newFile.Attributes.HasFlag(FileAttributes.Directory)) {
                            new DirectoryInfo(newFile.FullName).Delete(true);
                        }
                        else {
                            newFile.Delete();
                        }
                    }

                    if (oldFile.Exists) {
                        oldFile.MoveTo(newFile.FullName);
                    }
                }
            }
            SendStatusReport(0);
            return DLResultCode.MessageComplete;
        }

        private DLResultCode OnProcessMessage(ArrayNode msg)
        {
            DictionaryNode tmp = msg[1].AsDictionaryNode();
            int resultCode = (int) tmp["ErrorCode"].AsIntegerNode().Value;

            if (resultCode != 0 && tmp.TryGetValue("ErrorDescription", out PropertyNode? errorDescriptionNode)) {
                Logger.LogError("ProcessMessage {code}: {description}", resultCode, errorDescriptionNode.AsStringNode().Value);
            }

            switch (-resultCode) {
                case 0: {
                    return DLResultCode.Success;
                }
                case -38: {
                    Logger.LogError("Backing up the phone is denied by managing organisation");
                    return DLResultCode.BackupDeniedByOrganisation;
                }
                case -207: {
                    Logger.LogError("No backup encryption password set but is required by managing organisation");
                    return DLResultCode.MissingRequiredEncryptionPassword;
                }
                case -208: {
                    // Device locked which most commonly happens when requesting a backup but the user either
                    // hit cancel or the screen turned off again locking the phone and cancelling the backup.
                    Logger.LogError("Device locked: {error}", msg[1].AsDictionaryNode()["ErrorDescription"].AsStringNode().Value);
                    return DLResultCode.DeviceLocked;
                }
                default: {
                    Logger.LogError("Issue with OnProcessMessage: {code}", resultCode);
                    DictionaryNode msgDict = msg[1].AsDictionaryNode();
                    if (msgDict.TryGetValue("ErrorDescription", out PropertyNode? errDescription)) {
                        Logger.LogError("Description: {description}", errDescription.AsStringNode().Value);
                    }
                    return DLResultCode.UnexpectedError;
                }
            }
        }

        /// <summary>
        /// Manages the RemoveItems device message.
        /// </summary>
        /// <param name="msg">The message received from the device.</param>
        /// <returns>The number of items removed.</returns>
        private DLResultCode OnRemoveItems(ArrayNode msg, string rootPath, CancellationToken cancellationToken)
        {
            UpdateProgressForMessage(msg, 3);
            ArrayNode removes = msg[1].AsArrayNode();
            foreach (StringNode filename in removes.Cast<StringNode>()) {
                if (cancellationToken.IsCancellationRequested) {
                    break;
                }

                if (string.IsNullOrEmpty(filename.Value)) {
                    Logger.LogWarning("Empty file to remove.");
                }
                else {
                    FileInfo file = new FileInfo(Path.Combine(rootPath, filename.Value));
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
            SendStatusReport(0, string.Empty);
            return DLResultCode.MessageComplete;
        }

        /// <summary>
        /// Manages the UploadFiles device message.
        /// </summary>
        /// <param name="msg">The message received from the device.</param>
        /// <returns>The number of files processed.</returns>
        private async Task<DLResultCode> OnUploadFiles(ArrayNode msg, string rootPath, CancellationToken cancellationToken)
        {
            UpdateProgressForMessage(msg, 2);

            string errorDescription = string.Empty;
            int fileCount = 0;
            int errorCode = 0;

            long backupRealSize = 0;
            long backupTotalSize = (long) msg[3].AsIntegerNode().Value;
            if (backupTotalSize > 0) {
                Logger.LogDebug("Backup total size: {backupSize}", backupTotalSize);
            }

            while (!cancellationToken.IsCancellationRequested) {
                string devicePath = await ReceiveFilename(cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrEmpty(devicePath)) {
                    break;
                }
                else if (!Usbmux.IsDeviceConnected(Lockdown.Udid)) {
                    throw new DeviceDisconnectedException();
                }

                string backupPath = await ReceiveFilename(cancellationToken).ConfigureAwait(false);
                string localPath = Path.Combine(rootPath, backupPath);

                Logger.LogDebug("Receiving file {backupFilePath}", backupPath);
                DLResultCode code = await ReceiveFile(localPath, backupTotalSize, backupRealSize, cancellationToken).ConfigureAwait(false);
                if (code == DLResultCode.Success) {
                    DLFileReceived?.Invoke(this, new DLFileEventArgs(localPath));
                    if (string.Equals("Status.plist", Path.GetFileName(localPath), StringComparison.OrdinalIgnoreCase)) {
                        using (FileStream fs = File.OpenRead(localPath)) {
                            PropertyNode statusPlist = await PropertyList.LoadAsync(fs).ConfigureAwait(false);
                            OnStatusReceived(statusPlist.AsDictionaryNode());
                        }
                    }
                }
                fileCount++;
            }

            SendStatusReport(errorCode, errorDescription);
            return DLResultCode.MessageComplete;
        }

        private async Task<DLResultCode> ReadCode(CancellationToken cancellationToken)
        {
            byte[] buffer = await Service.ReceiveAsync(1, cancellationToken).ConfigureAwait(false);
            if (!Enum.IsDefined(typeof(DLResultCode), buffer[0])) {
                Logger.LogWarning("New backup code found: {code}", buffer[0]);
            }
            return (DLResultCode) buffer[0];
        }

        /// <summary>
        /// Receives a single file from the device.
        /// </summary>
        /// <param name="file">The BackupFile to receive.</param>
        /// <param name="totalSize">The total size indicated in the device message.</param>
        /// <param name="realSize">The actual bytes transferred.</param>
        /// <returns>The result code of the transfer.</returns>
        private async Task<DLResultCode> ReceiveFile(string localFilePath, long totalSize, long realSize, CancellationToken cancellationToken)
        {
            const int bufferLen = 32 * 1024;
            DLResultCode lastCode = DLResultCode.Success;
            if (File.Exists(localFilePath)) {
                File.Delete(localFilePath);
            }
            while (!cancellationToken.IsCancellationRequested) {
                // Size is the number of bytes left to read
                byte[] buffer = await Service.ReceiveAsync(sizeof(int), cancellationToken).ConfigureAwait(false);
                int size = EndianBitConverter.BigEndian.ToInt32(buffer, 0);
                if (size <= 0) {
                    break;
                }

                DLResultCode code = await ReadCode(cancellationToken).ConfigureAwait(false);
                int blockSize = size - sizeof(DLResultCode);
                if (code != DLResultCode.FileData) {
                    if (code == DLResultCode.Success) {
                        return code;
                    }

                    string msg = string.Empty;
                    if (blockSize > 0) {
                        byte[] msgBuffer = await Service.ReceiveAsync(blockSize, cancellationToken).ConfigureAwait(false);
                        msg = Encoding.UTF8.GetString(msgBuffer);
                    }

                    // iOS 17 beta devices seem to give RemoteError for a fair number of file now?
                    Logger.LogWarning("Failed to fully upload {localPath}. Reason: {msg}", localFilePath, msg);
                    DLFileTransferError?.Invoke(this, new DLFileEventArgs(localFilePath));
                    return code;
                }
                lastCode = code;

                int done = 0;
                while (done < blockSize) {
                    int toRead = Math.Min(blockSize - done, bufferLen);
                    buffer = await Service.ReceiveAsync(toRead, cancellationToken).ConfigureAwait(false);

                    // Ensure the directory requested exists before writing to it.
                    string? pathDir = Path.GetDirectoryName(localFilePath);
                    if (!string.IsNullOrWhiteSpace(pathDir) && !Directory.Exists(localFilePath)) {
                        Directory.CreateDirectory(pathDir);
                    }

                    using (FileStream stream = File.OpenWrite(localFilePath)) {
                        stream.Seek(0, SeekOrigin.End);
                        await stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
                    }
                    done += buffer.Length;
                }
                if (done == blockSize) {
                    realSize += blockSize;
                }
            }

            return lastCode;
        }

        /// <summary>
        /// Reads a filename from the backup service stream.
        /// </summary>
        /// <returns>The filename read from the backup stream, or NULL if there are no more files.</returns>
        private async Task<string> ReceiveFilename(CancellationToken cancellationToken)
        {
            byte[] buffer = await Service.ReceiveAsync(sizeof(int), cancellationToken).ConfigureAwait(false);
            int filenameLength = EndianBitConverter.BigEndian.ToInt32(buffer, 0);

            // A zero length means no more files to receive.
            if (filenameLength != 0) {
                buffer = await Service.ReceiveAsync(filenameLength, cancellationToken).ConfigureAwait(false);
                return Encoding.UTF8.GetString(buffer);
            }
            return string.Empty;
        }

        protected async Task<ArrayNode> DeviceLinkReceiveMessage(CancellationToken cancellationToken)
        {
            PropertyNode? message = await Service.ReceivePlistAsync(cancellationToken);
            if (message == null) {
                return new ArrayNode();
            }
            return message.AsArrayNode();
        }

        protected void DeviceLinkSend(PropertyNode message)
        {
            Service.SendPlist(message, PlistFormat.Binary);
        }

        /// <summary>
        /// Sends a DLMessagePing plist.
        /// </summary>
        /// <param name="message">String to send as ping message.</param>
        protected void DeviceLinkSendPing(string message)
        {
            ArrayNode msg = new ArrayNode() {
                new StringNode("DLMessagePing"),
                new StringNode(message)
            };
            DeviceLinkSend(msg);
        }

        protected void DeviceLinkSendProcessMessage(PropertyNode message)
        {
            Service.SendPlist(new ArrayNode() {
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
        protected async Task DeviceLinkVersionExchange(ulong versionMajor, ulong versionMinor, CancellationToken cancellationToken)
        {
            // Get DLMessageVersionExchange from device
            ArrayNode versionExchangeMessage = await DeviceLinkReceiveMessage(cancellationToken);
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
            Service.SendPlist(new ArrayNode {
                new StringNode("DLMessageVersionExchange"),
                new StringNode("DLVersionsOk"),
                new IntegerNode(versionMajor)
            }, PlistFormat.Binary);

            // Receive DeviceReady message
            ArrayNode messageDeviceReady = await DeviceLinkReceiveMessage(cancellationToken);
            dlMessage = messageDeviceReady[0].AsStringNode().Value;
            if (string.IsNullOrEmpty(dlMessage) || dlMessage != "DLMessageDeviceReady") {
                throw new DeviceLinkException("Device link didn't return ready state (DLMessageDeviceReady)");
            }
        }

        /// <summary>
        /// Sends a status report to the backup service.
        /// </summary>
        /// <param name="errorCode">The error code to send (as errno value).</param>
        /// <param name="errorMessage">The error message to send.</param>
        /// <param name="errorList">A PropertyNode with additional value(s).</param>
        protected void SendStatusReport(int errorCode, string? errorMessage = null, PropertyNode? errorList = null)
        {
            ArrayNode array = new ArrayNode {
                new StringNode("DLMessageStatusResponse"),
                new IntegerNode(errorCode)
            };

            if (errorMessage != null) {
                array.Add(new StringNode(errorMessage));
            }
            else {
                array.Add(new StringNode("___EmptyParameterString___"));
            }

            if (errorList != null) {
                array.Add(errorList);
            }
            else {
                array.Add(new DictionaryNode());
            }

            DeviceLinkSend(array);
        }

        public override void Dispose()
        {
            Disconnect();
            Close();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Process the message received from the backup service.
        /// </summary>
        /// <param name="msg">The property array received.</param>
        /// <param name="message">The string that identifies the message type.</param>
        /// <returns>Depends on the message type, but a negative value always indicates an error.</returns>
        public async Task<DLResultCode> OnDeviceLinkMessageReceived(ArrayNode msg, string message, string rootPath, CancellationToken cancellationToken = default)
        {
            Logger.LogDebug("Message Received: {message}", message);
            switch (message) {
                case DeviceLinkMessage.DownloadFiles: {
                    return await OnDownloadFiles(msg, rootPath, cancellationToken).ConfigureAwait(false);
                }
                case DeviceLinkMessage.GetFreeDiskSpace: {
                    return OnGetFreeDiskSpace(rootPath);
                }
                case DeviceLinkMessage.CreateDirectory: {
                    return OnCreateDirectory(msg, rootPath);
                }
                case DeviceLinkMessage.UploadFiles: {
                    return await OnUploadFiles(msg, rootPath, cancellationToken).ConfigureAwait(false);
                }
                case DeviceLinkMessage.ContentsOfDirectory: {
                    return OnListDirectory(msg, rootPath, cancellationToken);
                }
                case DeviceLinkMessage.MoveFiles:
                case DeviceLinkMessage.MoveItems: {
                    return OnMoveItems(msg, rootPath, cancellationToken);
                }
                case DeviceLinkMessage.RemoveFiles:
                case DeviceLinkMessage.RemoveItems: {
                    return OnRemoveItems(msg, rootPath, cancellationToken);
                }
                case DeviceLinkMessage.CopyItem: {
                    return OnCopyItem(msg, rootPath);
                }
                case DeviceLinkMessage.Disconnect: {
                    throw new DeviceDisconnectedException();
                }
                case DeviceLinkMessage.ProcessMessage: {
                    return OnProcessMessage(msg);
                }
                default: {
                    Logger.LogWarning("Unknown message in MessageLoop: {message}", message);
                    SendStatusReport(1, "Operation not supported");
                    return DLResultCode.UnknownMessage;
                }
            }
        }

        /// <summary>
        /// Sends the specified error report to the backup service.
        /// </summary>
        /// <param name="error">The error report to send.</param>
        public async Task SendError(DictionaryNode errorReport, CancellationToken cancellationToken)
        {
            byte[] errBytes = Encoding.UTF8.GetBytes(errorReport["DLFileErrorString"].AsStringNode().Value);
            List<byte> buffer = new List<byte> {
                (byte) ResultCode.LocalError
            };
            buffer.AddRange(errBytes);
            await SendPrefixed(buffer.ToArray(), buffer.Count, cancellationToken).ConfigureAwait(false);
        }
    }
}

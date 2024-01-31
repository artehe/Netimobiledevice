using Netimobiledevice.Afc;
using Netimobiledevice.Diagnostics;
using Netimobiledevice.EndianBitConversion;
using Netimobiledevice.Exceptions;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Lockdown.Services;
using Netimobiledevice.NotificationProxy;
using Netimobiledevice.Plist;
using Netimobiledevice.SpringBoardServices;
using Netimobiledevice.Usbmuxd;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Backup
{
    public class DeviceBackup : IDisposable
    {
        /// <summary>
        /// iTunes files to be inserted into the Info.plist file.
        /// </summary>
        private static readonly string[] iTunesFiles = new string[] {
            "ApertureAlbumPrefs",
            "IC-Info.sidb",
            "IC-Info.sidv",
            "PhotosFolderAlbums",
            "PhotosFolderName",
            "PhotosFolderPrefs",
            "VoiceMemos.plist",
            "iPhotoAlbumPrefs",
            "iTunesApplicationIDs",
            "iTunesPrefs",
            "iTunesPrefs.plist"
        };

        /// <summary>
        /// The AFC service.
        /// </summary>
        private AfcService? afcService;
        /// <summary>
        /// The last backup status received.
        /// </summary>
        private BackupStatus? lastStatus;
        /// <summary>
        /// The Notification service.
        /// </summary>
        private NotificationProxyService? notificationProxyService;
        /// <summary>
        /// The current snapshot state for the backup.
        /// </summary>
        private SnapshotState snapshotState = SnapshotState.Uninitialized;
        /// <summary>
        /// The sync lock identifier.
        /// </summary>
        private ulong syncLock;
        /// <summary>
        /// Indicates whether the device was disconnected during the backup process.
        /// </summary>
        protected bool deviceDisconnected;
        /// <summary>
        /// The backup service.
        /// </summary>
        protected Mobilebackup2Service? mobilebackup2Service;
        /// <summary>
        /// The exception that caused the backup to fail.
        /// </summary>
        protected Exception? terminatingException;
        /// <summary>
        /// Indicates whether the user cancelled the backup process.
        /// </summary>
        protected bool userCancelled;
        /// <summary>
        /// A list of the files whose transfer failed due to a device error.
        /// </summary>
        protected readonly List<BackupFile> failedFiles = new List<BackupFile>();

        protected bool IsFinished { get; set; }
        /// <summary>
        /// The Lockdown client.
        /// </summary>
        private LockdownClient LockdownClient { get; }
        /// <summary>
        /// The flag for cancelling the backup process.
        /// </summary>
        protected bool IsCancelling { get; set; }
        /// <summary>
        /// Indicates whether the backup is encrypted.
        /// </summary>
        public bool IsEncrypted { get; protected set; }
        public bool IsStopping => IsCancelling || IsFinished;
        /// <summary>
        /// The path to the backup folder, without the device UDID.
        /// </summary>
        public string BackupDirectory { get; }
        /// <summary>
        /// The path to the backup folder, including the device UDID.
        /// </summary>
        public string DeviceBackupPath { get; }
        /// <summary>
        /// Indicates whether the backup is currently in progress.
        /// </summary>
        public bool InProgress { get; protected set; }
        /// <summary>
        /// Indicates the backup progress, in a 0 to 100,000 range (in order to obtain a smoother integer progress).
        /// </summary>
        public double ProgressPercentage { get; protected set; }
        /// <summary>
        /// The time at which the backup started.
        /// </summary>
        public DateTime StartTime { get; protected set; }

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
        public event EventHandler<ErrorEventArgs>? Error;
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
        /// Creates an instance of a BackupJob class.
        /// </summary>
        /// <param name="lockdown">The lockdown client for the device that will be backed-up.</param>
        /// <param name="backupFolder">The folder to store the backup data. Without the device UDID.</param>
        public DeviceBackup(LockdownClient lockdown, string backupFolder)
        {
            LockdownClient = lockdown;
            BackupDirectory = backupFolder;
            DeviceBackupPath = Path.Combine(BackupDirectory, lockdown.UDID);
        }

        /// <summary>
        /// Destructor of the BackupJob class.
        /// </summary>
        ~DeviceBackup()
        {
            Dispose();
        }

        private async Task AquireBackupLock()
        {
            notificationProxyService?.Post(SendableNotificaton.SyncWillStart);
            syncLock = afcService?.FileOpen("/com.apple.itunes.lock_sync", "r+") ?? 0;

            if (syncLock != 0) {
                notificationProxyService?.Post(SendableNotificaton.SyncLockRequest);
                for (int i = 0; i < 50; i++) {
                    bool lockAquired = false;
                    try {
                        afcService?.Lock(syncLock, AfcLockModes.ExclusiveLock);
                        lockAquired = true;
                    }
                    catch (AfcException e) {
                        if (e.AfcError == AfcError.OpWouldBlock) {
                            await Task.Delay(200);
                        }
                        else {
                            afcService?.FileClose(syncLock);
                            throw;
                        }
                    }
                    catch (Exception) {
                        throw;
                    }

                    if (lockAquired) {
                        notificationProxyService?.Post(SendableNotificaton.SyncDidStart);
                        break;
                    }
                }
            }
            else {
                // Lock failed
                afcService?.FileClose(syncLock);
                throw new Exception("Failed to lock iTunes backup sync file");
            }
        }

        /// <summary>
        /// Cleans the used resources.
        /// </summary>
        private void CleanResources()
        {
            if (InProgress) {
                IsCancelling = true;
            }

            try {
                Unlock();
            }
            catch (ObjectDisposedException) {
                Debug.WriteLine("Object already disposed so I assume we can just continue");
            }
            catch (IOException) {
                Debug.WriteLine("Had an IO exception but I assume we can just continue");
            }

            notificationProxyService?.Dispose();
            mobilebackup2Service?.Dispose();
            afcService?.Dispose();
            InProgress = false;
        }

        /// <summary>
        /// Backup creation task entry point.
        /// </summary>
        private async Task CreateBackup(CancellationToken cancellationToken)
        {
            Debug.WriteLine($"Starting backup of device {LockdownClient.GetValue("ProductType")?.AsStringNode().Value} v{LockdownClient.IOSVersion}");

            // Reset everything in case we have called this more than once.
            lastStatus = null;
            InProgress = true;
            IsCancelling = false;
            IsFinished = false;
            userCancelled = false;
            deviceDisconnected = false;
            StartTime = DateTime.Now;
            ProgressPercentage = 0.0;
            terminatingException = null;
            snapshotState = SnapshotState.Uninitialized;

            Debug.WriteLine($"Saving at {DeviceBackupPath}");

            IsEncrypted = LockdownClient.GetValue("com.apple.mobile.backup", "WillEncrypt")?.AsBooleanNode().Value ?? false;
            Debug.WriteLine($"The backup will{(IsEncrypted ? null : " not")} be encrypted.");

            try {
                afcService = new AfcService(LockdownClient);
                mobilebackup2Service = await Mobilebackup2Service.CreateAsync(LockdownClient, cancellationToken);
                notificationProxyService = new NotificationProxyService(LockdownClient);

                await AquireBackupLock();

                OnStatus("Initializing backup ...");
                DictionaryNode options = CreateBackupOptions();
                mobilebackup2Service.SendRequest("Backup", LockdownClient.UDID, LockdownClient.UDID, options);

                if (IsPasscodeRequiredBeforeBackup()) {
                    PasscodeRequiredForBackup?.Invoke(this, EventArgs.Empty);
                }

                await MessageLoop(cancellationToken);
            }
            catch (Exception ex) {
                OnError(ex);
                return;
            }
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

        /// <summary>
        /// Creates the Info.plist dictionary.
        /// </summary>
        /// <returns>The created Info.plist as a DictionaryNode.</returns>
        private async Task<DictionaryNode> CreateInfoPlist()
        {
            DictionaryNode info = new DictionaryNode();

            (DictionaryNode appDict, ArrayNode installedApps) = await CreateInstalledAppList();
            info.Add("Applications", appDict);

            DictionaryNode? rootNode = LockdownClient.GetValue()?.AsDictionaryNode();
            if (rootNode != null) {
                info.Add("Build Version", rootNode["BuildVersion"]);
                info.Add("Device Name", rootNode["DeviceName"]);
                info.Add("Display Name", rootNode["DeviceName"]);
                info.Add("GUID", new StringNode(Guid.NewGuid().ToString()));

                if (rootNode.ContainsKey("IntegratedCircuitCardIdentity")) {
                    info.Add("ICCID", rootNode["IntegratedCircuitCardIdentity"]);
                }
                if (rootNode.ContainsKey("InternationalMobileEquipmentIdentity")) {
                    info.Add("IMEI", rootNode["InternationalMobileEquipmentIdentity"]);
                }

                info.Add("Installed Applications", installedApps);
                info.Add("Last Backup Date", new DateNode(StartTime));

                if (rootNode.ContainsKey("MobileEquipmentIdentifier")) {
                    info.Add("MEID", rootNode["MobileEquipmentIdentifier"]);
                }
                if (rootNode.ContainsKey("PhoneNumber")) {
                    info.Add("Phone Number", rootNode["PhoneNumber"]);
                }

                info.Add("Product Type", rootNode["ProductType"]);
                info.Add("Product Version", rootNode["ProductVersion"]);
                info.Add("Serial Number", rootNode["SerialNumber"]);

                info.Add("Target Identifier", new StringNode(LockdownClient.UDID.ToUpperInvariant()));
                info.Add("Target Type", new StringNode("Device"));
                info.Add("Unique Identifier", new StringNode(LockdownClient.UDID.ToUpperInvariant()));
            }

            try {
                byte[] dataBuffer = afcService?.GetFileContents("/Books/iBooksData2.plist") ?? Array.Empty<byte>();
                info.Add("iBooks Data 2", new DataNode(dataBuffer));
            }
            catch (AfcException ex) {
                if (ex.AfcError != AfcError.ObjectNotFound) {
                    throw;
                }
            }

            DictionaryNode files = new DictionaryNode();
            foreach (string iTuneFile in iTunesFiles) {
                try {
                    string filePath = Path.Combine("/iTunes_Control/iTunes", iTuneFile);
                    byte[] dataBuffer = afcService?.GetFileContents(filePath) ?? Array.Empty<byte>();
                    files.Add(iTuneFile, new DataNode(dataBuffer));
                }
                catch (AfcException ex) {
                    if (ex.AfcError == AfcError.ObjectNotFound) {
                        continue;
                    }
                    else {
                        throw;
                    }
                }
            }
            info.Add("iTunes Files", files);

            PropertyNode? itunesSettings = LockdownClient.GetValue("com.apple.iTunes", null);
            info.Add("iTunes Settings", itunesSettings ?? new DictionaryNode());

            // If we don't have iTunes, then let's get the minimum required iTunes version from the device
            PropertyNode? minItunesVersion = LockdownClient.GetValue("com.apple.mobile.iTunes", "MinITunesVersion");
            info.Add("iTunes Version", minItunesVersion ?? new StringNode("10.0.1"));

            return info;
        }

        /// <summary>
        /// Creates the application array and dictionary for the Info.plist file.
        /// </summary>
        /// <returns>The application dictionary and array of applications bundle ids.</returns>
        private async Task<(DictionaryNode, ArrayNode)> CreateInstalledAppList()
        {
            DictionaryNode appDict = new DictionaryNode();
            ArrayNode installedApps = new ArrayNode();

            using (InstallationProxyService installationProxyService = new InstallationProxyService(LockdownClient)) {
                using (SpringBoardServicesService springBoardServicesService = new SpringBoardServicesService(LockdownClient)) {
                    try {
                        ArrayNode apps = await installationProxyService.Browse(
                        new DictionaryNode() { { "ApplicationType", new StringNode("User") } },
                        new ArrayNode() { new StringNode("CFBundleIdentifier"), new StringNode("ApplicationSINF"), new StringNode("iTunesMetadata") });
                        foreach (DictionaryNode app in apps.Cast<DictionaryNode>()) {
                            if (app.ContainsKey("CFBundleIdentifier")) {
                                StringNode bundleId = app["CFBundleIdentifier"].AsStringNode();
                                installedApps.Add(bundleId);
                                if (app.ContainsKey("iTunesMetadata") && app.ContainsKey("ApplicationSINF")) {
                                    appDict.Add(bundleId.Value, new DictionaryNode() {
                                        { "ApplicationSINF", app["ApplicationSINF"] },
                                        { "iTunesMetadata", app["iTunesMetadata"] },
                                        { "PlaceholderIcon", springBoardServicesService.GetIconPNGData(bundleId.Value) },
                                    });
                                }
                            }
                        }
                    }
                    catch (Exception ex) {
                        Debug.WriteLine($"ERROR: Creating application list for Info.plist");
                        Debug.WriteLine(ex);
                    }
                }
            }
            return (appDict, installedApps);
        }

        private bool IsPasscodeRequiredBeforeBackup()
        {
            // iOS versions 15.7.1 and anything 16.1 or newer will require you to input a passcode before
            // it can start a backup so we make sure to notify the user about this.
            if ((LockdownClient.IOSVersion >= new Version(15, 7, 1) && LockdownClient.IOSVersion < new Version(16, 0)) ||
                LockdownClient.IOSVersion >= new Version(16, 1)) {
                using (DiagnosticsService diagnosticsService = new DiagnosticsService(LockdownClient)) {
                    string queryString = "PasswordConfigured";
                    try {
                    DictionaryNode queryResponse = diagnosticsService.MobileGestalt(new List<string>() { queryString });
                        if (queryResponse.TryGetValue(queryString, out PropertyNode? passcodeSetNode)) {
                            bool passcodeSet = passcodeSetNode.AsBooleanNode().Value;
                            if (passcodeSet) {
                                return true;
                            }
                        }
                    }
                    catch (DeprecatedException) {
                        // Assume that the passcode is set for now
                        // TODO Try and find a new way to tell if the devices passcode is set 
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// The main loop for processing messages from the device.
        /// </summary>
        private async Task MessageLoop(CancellationToken cancellationToken)
        {
            bool isFirstMessage = true;

            Debug.WriteLine("Starting the backup message loop.");
            while (!IsStopping) {
                try {
                    if (mobilebackup2Service != null) {
                        ArrayNode msg = await mobilebackup2Service.ReceiveMessage(cancellationToken);
                        if (msg != null) {
                            // Reset waiting state
                            if (snapshotState == SnapshotState.Waiting) {
                                OnSnapshotStateChanged(snapshotState, snapshotState = lastStatus?.SnapshotState ?? SnapshotState.Waiting);
                            }

                            // If it's the first message that isn't null report that the backup is started
                            if (isFirstMessage) {
                                OnBackupStarted();
                                await SaveInfoPropertyList();
                                isFirstMessage = false;
                            }

                            try {
                                OnMessageReceived(msg, msg[0].AsStringNode().Value);
                            }
                            catch (Exception ex) {
                                OnError(ex);
                            }
                        }
                        else if (!Usbmux.IsDeviceConnected(LockdownClient.UDID)) {
                            throw new DeviceDisconnectedException();
                        }
                    }
                }
                catch (TimeoutException) {
                    OnSnapshotStateChanged(snapshotState, SnapshotState.Waiting);
                    OnStatus("Waiting for device to be ready ...");
                    await Task.Delay(100, cancellationToken);
                }
                catch (Exception ex) {
                    Debug.WriteLine($"ERROR Receiving message");
                    OnError(ex);
                    break;
                }
            }

            // Check if the execution arrived here due to a device disconnection.
            if (terminatingException == null && !Usbmux.IsDeviceConnected(LockdownClient.UDID)) {
                throw new DeviceDisconnectedException();
            }

            Debug.WriteLine($"Has error: {terminatingException != null}");
            Debug.WriteLine($"Finished message loop. Cancelling = {IsCancelling}, Finished = {IsFinished}");
            OnBackupCompleted();
        }

        /// <summary>
        /// Manages the DownloadFiles device message.
        /// </summary>
        /// <param name="msg">The message received from the device.</param>
        private void OnDownloadFiles(ArrayNode msg)
        {
            UpdateProgressForMessage(msg, 3);

            DictionaryNode errList = new DictionaryNode();
            ArrayNode files = msg[1].AsArrayNode();
            foreach (StringNode filename in files.Cast<StringNode>()) {
                if (IsStopping) {
                    break;
                }
                else {
                    SendFile(filename.Value, errList);
                }
            }

            if (!IsStopping) {
                byte[] fileTransferTerminator = new byte[4];
                mobilebackup2Service?.SendRaw(fileTransferTerminator);
                if (errList.Count == 0) {
                    mobilebackup2Service?.SendStatusReport(0, null, null);
                }
                else {
                    mobilebackup2Service?.SendStatusReport(-13, "Multi status", errList);
                }
            }
        }

        /// <summary>
        /// Process the message received from the backup service.
        /// </summary>
        /// <param name="msg">The property array received.</param>
        /// <param name="message">The string that identifies the message type.</param>
        /// <returns>Depends on the message type, but a negative value always indicates an error.</returns>
        private void OnMessageReceived(ArrayNode msg, string message)
        {
            Debug.WriteLine($"Message Received: {message}");
            switch (message) {
                case DeviceLinkMessage.DownloadFiles: {
                    OnDownloadFiles(msg);
                    break;
                }
                case DeviceLinkMessage.GetFreeDiskSpace: {
                    OnGetFreeDiskSpace(msg);
                    break;
                }
                case DeviceLinkMessage.CreateDirectory: {
                    OnCreateDirectory(msg);
                    break;
                }
                case DeviceLinkMessage.UploadFiles: {
                    OnUploadFiles(msg);
                    break;
                }
                case DeviceLinkMessage.ContentsOfDirectory: {
                    OnListDirectory(msg);
                    break;
                }
                case DeviceLinkMessage.MoveFiles:
                case DeviceLinkMessage.MoveItems: {
                    OnMoveItems(msg);
                    break;
                }
                case DeviceLinkMessage.RemoveFiles:
                case DeviceLinkMessage.RemoveItems: {
                    OnRemoveItems(msg);
                    break;
                }
                case DeviceLinkMessage.CopyItem: {
                    OnCopyItem(msg);
                    break;
                }
                case DeviceLinkMessage.Disconnect: {
                    IsCancelling = true;
                    break;
                }
                case DeviceLinkMessage.ProcessMessage: {
                    OnProcessMessage(msg);
                    break;
                }
                default: {
                    Debug.WriteLine($"WARNING: Unknown message in MessageLoop: {message}");
                    mobilebackup2Service?.SendStatusReport(1, "Operation not supported");
                    break;
                }
            }
        }

        private void OnProcessMessage(ArrayNode msg)
        {
            int resultCode = ProcessMessage(msg);
            switch (resultCode) {
                case 0: {
                    IsFinished = true;
                    break;
                }
                case -38: {
                    OnError(new Exception("Backing up the phone is denied by managing organisation"));
                    break;
                }
                case -207: {
                    OnError(new Exception("No backup encryption password set but is required by managing organisation"));
                    break;
                }
                case -208: {
                    // Device locked which most commonly happens when requesting a backup but the user either
                    // hit cancel or the screen turned off again locking the phone and cancelling the backup.
                    OnError(new Exception($"Device locked - {msg[1].AsDictionaryNode()["ErrorDescription"].AsStringNode().Value}"));
                    break;
                }
                default: {
                    Debug.WriteLine($"ERROR On ProcessMessage: {resultCode}");
                    DictionaryNode msgDict = msg[1].AsDictionaryNode();
                    if (msgDict.TryGetValue("ErrorDescription", out PropertyNode? errDescription)) {
                        throw new Exception($"Error {resultCode}: {errDescription.AsStringNode().Value}");
                    }
                    else {
                        throw new Exception($"Error {resultCode}");
                    }
                }
            }
        }

        /// <summary>
        /// Manages the UploadFiles device message.
        /// </summary>
        /// <param name="msg">The message received from the device.</param>
        /// <returns>The number of files processed.</returns>
        private void OnUploadFiles(ArrayNode msg)
        {
            string errorDescription = string.Empty;
            int fileCount = 0;
            int errorCode = 0;
            UpdateProgressForMessage(msg, 2);

            int nlen = 0;
            long backupRealSize = 0;
            long backupTotalSize = (long) msg[3].AsIntegerNode().Value;
            if (backupTotalSize > 0) {
                Debug.WriteLine($"Backup total size: {backupTotalSize}");
            }

            while (!IsStopping) {
                BackupFile? backupFile = ReceiveBackupFile();
                if (backupFile != null) {
                    Debug.WriteLine($"Receiving file {backupFile.BackupPath}");
                    OnBeforeReceivingFile(backupFile);
                    ResultCode code = ReceiveFile(backupFile, backupTotalSize, ref backupRealSize, out nlen);
                    if (code == ResultCode.Success) {
                        OnFileReceived(backupFile);
                    }
                    else if (code != ResultCode.Skipped) {
                        Debug.WriteLine($"ERROR Receiving {backupFile.BackupPath}: {code}");
                    }
                    fileCount++;
                }
                else if (Usbmux.IsDeviceConnected(LockdownClient.UDID, UsbmuxdConnectionType.Usb)) {
                    break;
                }
                else {
                    throw new DeviceDisconnectedException();
                }
            }

            if (!IsStopping) {
                // If there are leftovers to read, finish up cleanly.
                if (--nlen > 0) {
                    mobilebackup2Service?.ReceiveRaw(nlen);
                }
                mobilebackup2Service?.SendStatusReport(errorCode, errorDescription);
            }
        }


        /// <summary>
        /// Processes a message response received from the backup service.
        /// </summary>
        /// <param name="msg">The message received.</param>
        /// <returns>The result status code from the message.</returns>
        private static int ProcessMessage(ArrayNode msg)
        {
            DictionaryNode tmp = msg[1].AsDictionaryNode();
            int errorCode = (int) tmp["ErrorCode"].AsIntegerNode().Value;
            string errorDescription = tmp["ErrorDescription"].AsStringNode().Value;
            if (errorCode != 0) {
                Debug.WriteLine($"ERROR: Code: {errorCode} {errorDescription}");
            }
            return -errorCode;
        }

        /// <summary>
        /// Reads the information of the next file that the backup service will send.
        /// </summary>
        /// <returns>Returns the file information of the next file to download, or null if there are no more files to download.</returns>
        private BackupFile? ReceiveBackupFile()
        {
            int len = ReceiveFilename(out string devicePath);
            if (len == 0) {
                return null;
            }
            len = ReceiveFilename(out string backupPath);
            if (len <= 0) {
                Debug.WriteLine("WARNING Error reading backup file path.");
            }
            return new BackupFile(devicePath, backupPath, BackupDirectory);
        }

        /// <summary>
        /// Reads a filename from the backup service stream.
        /// </summary>
        /// <param name="filename">The filename read from the backup stream, or NULL if there are no more files.</param>
        /// <returns>The length of the filename read.</returns>
        private int ReceiveFilename(out string filename)
        {
            filename = string.Empty;
            int len = ReadInt32();

            // A zero length means no more files to receive.
            if (len != 0) {
                byte[] buffer = mobilebackup2Service?.ReceiveRaw(len) ?? Array.Empty<byte>();
                filename = Encoding.UTF8.GetString(buffer);
            }
            return len;
        }

        private ResultCode ReadCode()
        {
            byte[] buffer = mobilebackup2Service?.ReceiveRaw(1) ?? Array.Empty<byte>();

            byte code = buffer[0];
            if (!Enum.IsDefined(typeof(ResultCode), code)) {
                Debug.WriteLine($"WARNING: New backup code found: {code}");
            }
            ResultCode result = (ResultCode) code;

            return result;
        }

        /// <summary>
        /// Reads an Int32 value from the backup service.
        /// </summary>
        /// <returns>The Int32 value read.</returns>
        private int ReadInt32()
        {
            byte[] buffer = mobilebackup2Service?.ReceiveRaw(4) ?? Array.Empty<byte>();
            if (buffer.Length > 0) {
                return EndianBitConverter.BigEndian.ToInt32(buffer, 0);
            }
            return -1;
        }

        /// <summary>
        /// Sends a single file to the device.
        /// </summary>
        /// <param name="filename">The relative filename requested to send.</param>
        /// <param name="errList">The error list to append the eventual local error happening.</param>
        /// <returns>The errno result of the operation.</returns>
        private void SendFile(string filename, DictionaryNode errList)
        {
            Debug.WriteLine($"Sending file: {filename}");

            mobilebackup2Service?.SendPath(filename);
            string localFile = Path.Combine(BackupDirectory, filename);
            FileInfo fileInfo = new FileInfo(localFile);
            int errorCode;
            if (!fileInfo.Exists) {
                errorCode = 2;
            }
            else if (fileInfo.Length == 0) {
                errorCode = 0;
            }
            else {
                SendFile(fileInfo);
                errorCode = 0;
            }

            if (errorCode == 0) {
                List<byte> bytes = new List<byte>(EndianBitConverter.BigEndian.GetBytes(1)) {
                    (byte) ResultCode.Success
                };
                mobilebackup2Service?.SendRaw(bytes.ToArray());
            }
            else {
                Debug.WriteLine($"Sending Error Code: {errorCode}");
                DictionaryNode errReport = CreateErrorReport(errorCode);
                errList.Add(filename, errReport);
                mobilebackup2Service?.SendError(errReport);
            }
        }

        /// <summary>
        /// Sends the specified file to the device.
        /// </summary>
        /// <param name="fileInfo">The FileInfo of the file to send.</param>
        /// <returns>The MobileBackup2Error result of the native call.</sreturns>
        private void SendFile(FileInfo fileInfo)
        {
            const int maxBufferSize = 32768;
            long remaining = fileInfo.Length;
            using (FileStream stream = File.OpenRead(fileInfo.FullName)) {
                while (remaining > 0) {
                    int toSend = (int) Math.Min(maxBufferSize, remaining);
                    List<byte> bytes = new List<byte>(EndianBitConverter.BigEndian.GetBytes(toSend)) {
                        (byte) ResultCode.FileData
                    };
                    mobilebackup2Service?.SendRaw(bytes.ToArray());

                    byte[] buffer = new byte[toSend];
                    int read = stream.Read(buffer, 0, toSend);
                    mobilebackup2Service?.SendRaw(buffer);

                    remaining -= read;
                }
            }
        }

        /// <summary>
        /// Unlocks the sync file.
        /// </summary>
        private void Unlock()
        {
            if (syncLock != 0) {
                afcService?.Lock(syncLock, AfcLockModes.Unlock);
                syncLock = 0;
            }
        }

        /// <summary>
        /// Creates a dictionary with the options for the backup.
        /// </summary>
        /// <returns>A PropertyDict containing the backup options.</returns>
        protected virtual DictionaryNode CreateBackupOptions()
        {
            DictionaryNode options = new DictionaryNode {
                { "ForceFullBackup", new BooleanNode(true) }
            };
            return options;
        }

        /// <summary>
        /// Gets the free space on the disk containing the specified path.
        /// </summary>
        /// <param name="path">The path that specifies the disk to retrieve its free space.</param>
        /// <returns>The number of bytes of free space in the disk specified by path.</returns>
        protected virtual long GetFreeSpace(string path)
        {
            DirectoryInfo dir = new DirectoryInfo(path);
            foreach (DriveInfo drive in DriveInfo.GetDrives()) {
                try {
                    if (drive.IsReady && drive.Name == dir.Root.FullName) {
                        return drive.AvailableFreeSpace;
                    }
                }
                catch (Exception ex) {
                    Debug.WriteLine($"Error: {ex}");
                }
            }
            return 0;
        }

        /// <summary>
        /// Invoke the FileReceived event
        /// </summary>
        /// <param name="eventArgs">The BackupFileEventArgs for the file receiving event</param>
        protected void InvokeOnFileReceived(BackupFileEventArgs eventArgs)
        {
            FileReceived?.Invoke(this, eventArgs);
        }

        /// <summary>
        /// Invoke the FileReceiving event
        /// </summary>
        /// <param name="eventArgs">The BackupFileEventArgs for the file receiving event</param>
        protected void InvokeOnFileReceiving(BackupFileEventArgs eventArgs)
        {
            FileReceiving?.Invoke(this, eventArgs);
        }

        /// <summary>
        /// Event handler called when the backup is completed.
        /// </summary>
        protected virtual void OnBackupCompleted()
        {
            Debug.WriteLine("Device Backup Completed");
            Completed?.Invoke(this, new BackupResultEventArgs(failedFiles, userCancelled, deviceDisconnected));
        }

        /// <summary>
        /// Event handler called to report progress.
        /// </summary>
        /// <param name="filename">The filename related to the progress.</param>
        protected virtual void OnBackupProgress()
        {
            Progress?.Invoke(this, new ProgressChangedEventArgs((int) ProgressPercentage, null));
        }

        /// <summary>
        /// Event handler called when the backup has actually started.
        /// </summary>
        protected virtual void OnBackupStarted()
        {
            notificationProxyService?.Post(SendableNotificaton.SyncDidStart);
            Started?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Event handler called before a file is to be received from the device.
        /// </summary>
        /// <param name="file">The file to be received.</param>
        protected virtual void OnBeforeReceivingFile(BackupFile file)
        {
            BeforeReceivingFile?.Invoke(this, new BackupFileEventArgs(file));
        }

        /// <summary>
        /// Manages the CopyItem device message.
        /// </summary>
        /// <param name="msg">The message received from the device.</param>
        /// <returns>The errno result of the operation.</returns>
        protected virtual void OnCopyItem(ArrayNode msg)
        {
            int errorCode = 0;
            string errorDesc = string.Empty;
            string srcPath = Path.Combine(BackupDirectory, msg[1].AsStringNode().Value);
            string dstPath = Path.Combine(BackupDirectory, msg[2].AsStringNode().Value);

            FileInfo source = new FileInfo(srcPath);
            if (source.Attributes.HasFlag(FileAttributes.Directory)) {
                Debug.WriteLine($"ERROR: Are you really asking me to copy a whole directory?");
            }
            else {
                File.Copy(source.FullName, new FileInfo(dstPath).FullName);
            }
            mobilebackup2Service?.SendStatusReport(errorCode, errorDesc);
        }

        /// <summary>
        /// Manages the CreateDirectory device message.
        /// </summary>
        /// <param name="msg">The message received from the device.</param>
        /// <returns>The errno result of the operation.</returns>
        protected virtual void OnCreateDirectory(ArrayNode msg)
        {
            int errorCode = 0;
            string errorMessage = string.Empty;
            UpdateProgressForMessage(msg, 3);

            if (!Directory.Exists(DeviceBackupPath)) {
                Directory.CreateDirectory(DeviceBackupPath);
            }

            DirectoryInfo newDir = new DirectoryInfo(Path.Combine(BackupDirectory, msg[1].AsStringNode().Value));
            if (!newDir.Exists) {
                newDir.Create();
            }
            mobilebackup2Service?.SendStatusReport(errorCode, errorMessage);
        }

        /// <summary>
        /// Event handler called when a terminating error happens during the backup.
        /// </summary>
        /// <param name="ex"></param>
        protected virtual void OnError(Exception ex)
        {
            IsCancelling = true;
            deviceDisconnected = Usbmux.IsDeviceConnected(LockdownClient.UDID);
            Debug.WriteLine($"BackupJob.OnError: {ex.Message}");
            terminatingException = deviceDisconnected ? ex : new DeviceDisconnectedException();
            Error?.Invoke(this, new ErrorEventArgs(terminatingException));
        }

        /// <summary>
        /// Event handler called after a file has been received from the device.
        /// </summary>
        /// <param name="file">The file received.</param>
        protected virtual void OnFileReceived(BackupFile file)
        {
            FileReceived?.Invoke(this, new BackupFileEventArgs(file));
            if (string.Equals("Status.plist", Path.GetFileName(file.LocalPath), StringComparison.OrdinalIgnoreCase)) {
                using (FileStream fs = File.OpenRead(file.LocalPath)) {
                    DictionaryNode statusPlist = PropertyList.Load(fs).AsDictionaryNode();
                    OnStatusReceived(new BackupStatus(statusPlist));
                }
            }
        }

        /// <summary>
        /// Event handler called after a part (or all of) a file has been sent from the device from the device.
        /// </summary>
        /// <param name="file">The file received.</param>
        /// <param name="fileData">The file contents received</param>
        protected virtual void OnFileReceiving(BackupFile file, byte[] fileData)
        {
            InvokeOnFileReceiving(new BackupFileEventArgs(file, fileData));

            // Ensure the directory requested exists before writing to it.
            string? pathDir = Path.GetDirectoryName(file.LocalPath);
            if (!string.IsNullOrWhiteSpace(pathDir) && !Directory.Exists(file.LocalPath)) {
                Directory.CreateDirectory(pathDir);
            }

            using (FileStream stream = File.OpenWrite(file.LocalPath)) {
                stream.Seek(0, SeekOrigin.End);
                stream.Write(fileData, 0, fileData.Length);
            }
        }

        /// <summary>
        /// Event handler called after a file transfer failed due to a device error.
        /// </summary>
        /// <param name="file">The file whose tranfer failed.</param>
        protected virtual void OnFileTransferError(BackupFile file)
        {
            failedFiles.Add(file);
            if (FileTransferError != null) {
                BackupFileErrorEventArgs e = new BackupFileErrorEventArgs(file);
                FileTransferError.Invoke(this, e);
                IsCancelling = e.Cancel;
            }
        }

        /// <summary>
        /// Manages the GetFreeDiskSpace device message.
        /// </summary>
        /// <param name="msg">The message received from the device.</param>
        /// <param name="respectFreeSpaceValue">Whether the device should abide by the freeSpace value passed or ignore it</param>
        /// <returns>0 on success, -1 on error.</returns>
        protected virtual void OnGetFreeDiskSpace(ArrayNode msg, bool respectFreeSpaceValue = true)
        {
            long freeSpace = GetFreeSpace(BackupDirectory);
            IntegerNode spaceItem = new IntegerNode(freeSpace);
            if (respectFreeSpaceValue) {
                mobilebackup2Service?.SendStatusReport(0, null, spaceItem);
            }
            else {
                mobilebackup2Service?.SendStatusReport(-1, null, spaceItem);
            }
        }

        /// <summary>
        /// Manages the ListDirectory device message.
        /// </summary>
        /// <param name="msg">The message received from the device.</param>
        /// <returns>Always 0.</returns>
        protected virtual void OnListDirectory(ArrayNode msg)
        {
            string path = Path.Combine(BackupDirectory, msg[1].AsStringNode().Value);
            DictionaryNode dirList = new DictionaryNode();
            DirectoryInfo dir = new DirectoryInfo(path);
            if (dir.Exists) {
                foreach (FileSystemInfo entry in dir.GetFileSystemInfos()) {
                    if (IsStopping) {
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

            if (!IsStopping) {
                mobilebackup2Service?.SendStatusReport(0, null, dirList);
            }
        }

        /// <summary>
        /// Manages the MoveItems device message.
        /// </summary>
        /// <param name="msg">The message received from the device.</param>
        /// <returns>The number of items moved.</returns>
        protected virtual void OnMoveItems(ArrayNode msg)
        {
            int res = 0;
            int errorCode = 0;
            string errorDesc = string.Empty;
            UpdateProgressForMessage(msg, 3);
            foreach (KeyValuePair<string, PropertyNode> move in msg[1].AsDictionaryNode()) {
                if (IsStopping) {
                    break;
                }

                string newPath = move.Value.AsStringNode().Value;
                if (!string.IsNullOrEmpty(newPath)) {
                    res++;
                    FileInfo newFile = new FileInfo(Path.Combine(BackupDirectory, newPath));
                    FileInfo oldFile = new FileInfo(Path.Combine(BackupDirectory, move.Key));
                    FileInfo fileInfo = new FileInfo(newPath);
                    if (fileInfo.Exists) {
                        if (fileInfo.Attributes.HasFlag(FileAttributes.Directory)) {
                            new DirectoryInfo(newFile.FullName).Delete(true);
                        }
                        else {
                            fileInfo.Delete();
                        }
                    }

                    if (oldFile.Exists) {
                        oldFile.MoveTo(newFile.FullName);
                    }
                }
            }

            mobilebackup2Service?.SendStatusReport(errorCode, errorDesc);
        }

        /// <summary>
        /// Manages the RemoveItems device message.
        /// </summary>
        /// <param name="msg">The message received from the device.</param>
        /// <returns>The number of items removed.</returns>
        protected virtual void OnRemoveItems(ArrayNode msg)
        {
            UpdateProgressForMessage(msg, 3);

            int errorCode = 0;
            string errorDesc = string.Empty;
            ArrayNode removes = msg[1].AsArrayNode();
            foreach (StringNode filename in removes.Cast<StringNode>()) {
                if (IsStopping) {
                    break;
                }

                if (string.IsNullOrEmpty(filename.Value)) {
                    Debug.WriteLine("WARNING: Empty file to remove.");
                }
                else {
                    FileInfo file = new FileInfo(Path.Combine(BackupDirectory, filename.Value));
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

            if (!IsStopping) {
                mobilebackup2Service?.SendStatusReport(errorCode, errorDesc);
            }
        }

        /// <summary>
        /// Event handler called when the snapshot state of the backup changes.
        /// </summary>
        /// <param name="oldSnapshotState">The previous snapshot state.</param>
        /// <param name="newSnapshotState">The new snapshot state.</param>
        protected virtual void OnSnapshotStateChanged(SnapshotState oldSnapshotState, SnapshotState newSnapshotState)
        {
            Debug.WriteLine($"Snapshot state changed: {newSnapshotState}");
            OnStatus($"{newSnapshotState} ...");
            if (newSnapshotState == SnapshotState.Finished) {
                IsFinished = true;
            }
        }

        /// <summary>
        /// Event handler called to report a status messages.
        /// </summary>
        /// <param name="message">The status message to report.</param>
        protected virtual void OnStatus(string message)
        {
            Status?.Invoke(this, new StatusEventArgs(message));
            Debug.WriteLine($"OnStatus: {message}");
        }

        /// <summary>
        /// Event handler called each time the backup service sends a status report.
        /// </summary>
        /// <param name="status">The status report sent from the backup service.</param>
        protected virtual void OnStatusReceived(BackupStatus status)
        {
            lastStatus = status;
            if (snapshotState != status.SnapshotState) {
                OnSnapshotStateChanged(snapshotState, snapshotState = status.SnapshotState);
            }
        }

        /// <summary>
        /// Receives a single file from the device.
        /// </summary>
        /// <param name="file">The BackupFile to receive.</param>
        /// <param name="totalSize">The total size indicated in the device message.</param>
        /// <param name="realSize">The actual bytes transferred.</param>
        /// <param name="nlen">The number of bytes left to read.</param>
        /// <param name="skip">Indicates whether to skip or save the file.</param>
        /// <returns>The result code of the transfer.</returns>
        protected virtual ResultCode ReceiveFile(BackupFile file, long totalSize, ref long realSize, out int nlen, bool skip = false)
        {
            nlen = 0;
            const int bufferLen = 32 * 1024;
            ResultCode lastCode = ResultCode.Success;
            if (File.Exists(file.LocalPath)) {
                File.Delete(file.LocalPath);
            }
            while (!IsStopping) {
                nlen = ReadInt32();
                if (nlen <= 0) {
                    break;
                }

                ResultCode code = ReadCode();
                int blockSize = nlen - 1;
                if (code != ResultCode.FileData) {
                    if (code == ResultCode.Success) {
                        return code;
                    }
                    if (blockSize > 0) {
                        byte[] msgBuffer = mobilebackup2Service?.ReceiveRaw(blockSize) ?? Array.Empty<byte>();
                        string msg = Encoding.UTF8.GetString(msgBuffer);
                        Debug.WriteLine($"ERROR Receving file data: {code}: {msg}");
                    }
                    OnFileTransferError(file);
                    return code;
                }
                lastCode = code;

                int done = 0;
                while (done < blockSize) {
                    int toRead = Math.Min(blockSize - done, bufferLen);
                    byte[] buffer = mobilebackup2Service?.ReceiveRaw(toRead) ?? Array.Empty<byte>();
                    if (!skip) {
                        OnFileReceiving(file, buffer);
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
        /// Generates and saves the backup Info.plist file.
        /// </summary>
        protected virtual async Task SaveInfoPropertyList()
        {
            OnStatus("Creating Info.plist");
            BackupFile backupFile = new BackupFile(string.Empty, $"Info.plist", DeviceBackupPath);

            DateTime startTime = DateTime.Now;

            PropertyNode infoPlist = await CreateInfoPlist();
            byte[] infoPlistData = PropertyList.SaveAsByteArray(infoPlist, PlistFormat.Xml);
            OnFileReceiving(backupFile, infoPlistData);

            TimeSpan elapsed = DateTime.Now - startTime;
            Debug.WriteLine($"Creating Info.plist took {elapsed}");

            OnFileReceived(backupFile);
        }

        /// <summary>
        /// Updates the backup progress as signaled by the backup service.
        /// </summary>
        /// <param name="msg">The message received containing the progress information.</param>
        /// <param name="index">The index of the element in the array that contains the progress value.</param>
        protected void UpdateProgressForMessage(ArrayNode msg, int index)
        {
            double progress = msg[index].AsRealNode().Value;
            if (progress > 0.0) {
                ProgressPercentage = progress;
                OnBackupProgress();
            }
        }

        /// <summary>
        /// Disposes the used resources.
        /// </summary>
        public void Dispose()
        {
            CleanResources();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Starts the backup process.
        /// </summary>
        public async Task Start(CancellationToken cancellationToken = default)
        {
            if (!InProgress) {
                await CreateBackup(cancellationToken);
            }
        }

        /// <summary>
        /// Stops the backup process.
        /// </summary>
        public void Stop()
        {
            if (InProgress && !IsStopping) {
                IsCancelling = true;
                userCancelled = true;
            }
        }
    }
}

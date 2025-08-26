using Microsoft.Extensions.Logging;
using Netimobiledevice.Afc;
using Netimobiledevice.DeviceLink;
using Netimobiledevice.InstallationProxy;
using Netimobiledevice.Lockdown;
using Netimobiledevice.NotificationProxy;
using Netimobiledevice.Plist;
using Netimobiledevice.SpringBoardServices;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Backup
{
    /// <summary>
    /// Communication with the Backup service to either create or restore a backup as well as configuring the backup encryption password.
    /// </summary>
    /// <param name="lockdown"></param>
    /// <param name="logger"></param>
    public sealed class Mobilebackup2Service(LockdownServiceProvider lockdown, ILogger? logger = null) : LockdownService(lockdown, LOCKDOWN_SERVICE_NAME, RSD_SERVICE_NAME, useEscrowBag: true, logger: logger)
    {
        private const int MOBILEBACKUP2_VERSION_MAJOR = 400;
        private const int MOBILEBACKUP2_VERSION_MINOR = 0;

        private const string LOCKDOWN_SERVICE_NAME = "com.apple.mobilebackup2";
        private const string RSD_SERVICE_NAME = "com.apple.mobilebackup2.shim.remote";

        private CancellationTokenSource _internalCts = new CancellationTokenSource();
        private bool _passcodeRequired;

        /// <summary>
        /// iTunes files to be inserted into the Info.plist file.
        /// </summary>
        private static readonly string[] iTunesFiles = [
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
        ];

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
        public event EventHandler<BackupStartedEventArgs>? Started;
        /// <summary>
        /// Event raised for signaling different kinds of the backup status.
        /// </summary>
        public event EventHandler<StatusEventArgs>? Status;

        private static bool BackupExists(string backupDirectory, string identifier)
        {
            string deviceDirectory = Path.Combine(backupDirectory, identifier);
            bool infoPlistExists = File.Exists(Path.Combine(deviceDirectory, "Info.plist"));
            bool manifestPlistExists = File.Exists(Path.Combine(deviceDirectory, "Manifest.plist"));
            bool statusPlistExists = File.Exists(Path.Combine(deviceDirectory, "Status.plist"));
            return infoPlistExists && manifestPlistExists && statusPlistExists;
        }

        private async Task<ResultCode> ChangeBackupEncryptionPassword(string? oldPassword, string? newPassword, BackupEncryptionFlags flag, CancellationToken cancellationToken)
        {
            DictionaryNode backupDomain = Lockdown.GetValue("com.apple.mobile.backup", null)?.AsDictionaryNode() ?? [];
            backupDomain.TryGetValue("WillEncrypt", out PropertyNode? willEncryptNode);
            bool willEncryptBackup = willEncryptNode?.AsBooleanNode().Value ?? false;

            switch (flag) {
                case BackupEncryptionFlags.Enable: {
                    if (willEncryptBackup) {
                        Logger.LogError("ERROR Backup encryption is already enabled. Aborting.");
                        throw new InvalidOperationException("Can't set backup password as one already exists");
                    }
                    else if (string.IsNullOrEmpty(newPassword)) {
                        Logger.LogError("No backup password given. Aborting.");
                        throw new ArgumentException("password can't be null or empty");
                    }
                    break;
                }
                case BackupEncryptionFlags.ChangePassword: {
                    if (!willEncryptBackup) {
                        Logger.LogError("Error Backup encryption is not enabled so can't change password. Aborting");
                        throw new InvalidOperationException("Backup encryption isn't enabled so can't change password");
                    }
                    break;
                }
                case BackupEncryptionFlags.Disable: {
                    if (!willEncryptBackup) {
                        Logger.LogError("ERROR Backup encryption is already disabled. Aborting.");
                        throw new InvalidOperationException("Can't remove backup password as none exists");
                    }
                    else if (string.IsNullOrEmpty(oldPassword)) {
                        Logger.LogError("No backup password given. Aborting.");
                        throw new ArgumentException("password can't be null or empty");
                    }
                    break;
                }
            }

            if (string.IsNullOrEmpty(newPassword) && string.IsNullOrEmpty(oldPassword)) {
                throw new Mobilebackup2Exception("Both newPassword and oldPassword can't be null or empty");
            }

            using (DeviceLinkService dl = await GetDeviceLink(string.Empty, true, true, cancellationToken).ConfigureAwait(false)) {
                DictionaryNode message = new DictionaryNode() {
                    { "MessageName", new StringNode("ChangePassword") },
                    { "TargetIdentifier", new StringNode(Lockdown.Udid) },
                };
                if (!string.IsNullOrEmpty(oldPassword)) {
                    message.Add("OldPassword", new StringNode(oldPassword));
                }
                if (!string.IsNullOrEmpty(newPassword)) {
                    message.Add("NewPassword", new StringNode(newPassword));
                }
                dl.SendProcessMessage(message);
                return await dl.DlLoop(cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Creates the Info.plist dictionary.
        /// </summary>
        /// <returns>The created Info.plist as a DictionaryNode.</returns>
        private async Task<DictionaryNode> CreateInfoPlist(AfcService afc, CancellationToken cancellationToken)
        {
            DictionaryNode rootNode = Lockdown.GetValue()?.AsDictionaryNode() ?? [];
            PropertyNode? itunesSettings = Lockdown.GetValue("com.apple.iTunes", null);

            // Get the minimum required iTunes version from the device or use a specified default 
            PropertyNode minItunesVersion = Lockdown.GetValue("com.apple.mobile.iTunes", "MinITunesVersion") ?? new StringNode("10.0.1");

            DictionaryNode appDict = [];
            ArrayNode installedApps = [];
            using (InstallationProxyService installationProxyService = new InstallationProxyService(Lockdown)) {
                using (SpringBoardServicesService springBoardServicesService = new SpringBoardServicesService(Lockdown)) {
                    try {
                        ArrayNode apps = await installationProxyService.Browse(
                            new DictionaryNode() { { "ApplicationType", new StringNode("User") } },
                            [
                                new StringNode("CFBundleIdentifier"),
                                new StringNode("ApplicationSINF"),
                                new StringNode("iTunesMetadata")
                            ],
                            cancellationToken).ConfigureAwait(false);
                        foreach (DictionaryNode app in apps.Cast<DictionaryNode>()) {
                            if (app.TryGetValue("CFBundleIdentifier", out PropertyNode? bundleIdNode)) {
                                installedApps.Add(bundleIdNode);

                                string bundleId = bundleIdNode.AsStringNode().Value;
                                if (app.TryGetValue("ApplicationSINF", out PropertyNode? applicationSinfNode) && app.TryGetValue("iTunesMetadata", out PropertyNode? itunesMetadataNode)) {
                                    appDict.Add(bundleId, new DictionaryNode() {
                                        { "ApplicationSINF", applicationSinfNode },
                                        { "iTunesMetadata", itunesMetadataNode },
                                        { "PlaceholderIcon", springBoardServicesService.GetIconPNGData(bundleId) },
                                    });
                                }
                            }
                        }
                    }
                    catch (Exception ex) {
                        Logger.LogWarning(ex, "Failed to create application list for Info.plist");
                    }

                    DictionaryNode files = [];
                    foreach (string iTuneFile in iTunesFiles) {
                        string filePath = $"/iTunes_Control/iTunes/{iTuneFile}";
                        try {
                            byte[] dataBuffer = await afc.GetFileContents(filePath, cancellationToken) ?? [];
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

                    DictionaryNode info = new DictionaryNode {
                        { "iTunes Version", minItunesVersion },
                        { "iTunes Files", files },
                        { "Unique Identifier", new StringNode(Lockdown.Udid.ToUpperInvariant()) },
                        { "Target Type", new StringNode("Device") },
                        { "Target Identifier", rootNode["UniqueDeviceID"] },
                        { "Serial Number", rootNode["SerialNumber"] },
                        { "Product Version", rootNode["ProductVersion"] },
                        { "Product Type", rootNode["ProductType"] },
                        { "Installed Applications", installedApps },
                        { "GUID", new StringNode(Guid.NewGuid().ToString()) },
                        { "Display Name", rootNode["DeviceName"] },
                        { "Device Name", rootNode["DeviceName"] },
                        { "Build Version", rootNode["BuildVersion"] },
                        { "Applications", appDict },
                        { "Last Backup Date", new DateNode(DateTime.Now) }
                    };

                    if (rootNode.TryGetValue("IntegratedCircuitCardIdentity", out PropertyNode? iccidNode)) {
                        info.Add("ICCID", iccidNode);
                    }
                    if (rootNode.TryGetValue("InternationalMobileEquipmentIdentity", out PropertyNode? imeiNode)) {
                        info.Add("IMEI", imeiNode);
                    }
                    if (rootNode.TryGetValue("MobileEquipmentIdentifier", out PropertyNode? meidNode)) {
                        info.Add("MEID", meidNode);
                    }
                    if (rootNode.TryGetValue("PhoneNumber", out PropertyNode? phoneNumberNode)) {
                        info.Add("Phone Number", phoneNumberNode);
                    }

                    try {
                        byte[] dataBuffer = await afc.GetFileContents("/Books/iBooksData2.plist", cancellationToken).ConfigureAwait(false) ?? [];
                        info.Add("iBooks Data 2", new DataNode(dataBuffer));
                    }
                    catch (AfcException ex) {
                        if (ex.AfcError != AfcError.ObjectNotFound) {
                            throw;
                        }
                    }

                    if (itunesSettings != null) {
                        info.Add("iTunes Settings", itunesSettings ?? new DictionaryNode());
                    }

                    return info;
                }
            }
        }

        private void DeviceLink_BeforeReceivingFile(object? sender, BackupFileEventArgs e)
        {
            BeforeReceivingFile?.Invoke(sender, e);
        }

        private void DeviceLink_Completed(object? sender, BackupResultEventArgs e)
        {
            Completed?.Invoke(sender, e);
        }

        private void DeviceLink_Error(object? sender, ErrorEventArgs e)
        {
            Error?.Invoke(sender, e);
        }

        private void DeviceLink_FileReceived(object? sender, BackupFileEventArgs e)
        {
            FileReceived?.Invoke(sender, e);
        }

        private void DeviceLink_FileReceiving(object? sender, BackupFileEventArgs e)
        {
            FileReceiving?.Invoke(sender, e);
        }

        private void DeviceLink_FileTransferError(object? sender, BackupFileErrorEventArgs e)
        {
            FileTransferError?.Invoke(sender, e);
        }

        private void DeviceLink_PasscodeRequiredForBackup(object? sender, EventArgs e)
        {
            PasscodeRequiredForBackup?.Invoke(sender, e);
        }

        private void DeviceLink_Progress(object? sender, ProgressChangedEventArgs e)
        {
            Progress?.Invoke(sender, e);
        }

        private void DeviceLink_Status(object? sender, StatusEventArgs e)
        {
            Status?.Invoke(sender, e);
        }
        private void DeviceLink_Started(object? sender, BackupStartedEventArgs e)
        {
            Started?.Invoke(sender, e);
        }

        private async Task<DeviceLinkService> GetDeviceLink(string backupDirectory, bool ignoreTransferErrors, bool performBackupSizeCheck, CancellationToken cancellationToken)
        {
            DeviceLinkService dl = new DeviceLinkService(this.Service, backupDirectory, this.Lockdown.OsVersion, ignoreTransferErrors, performBackupSizeCheck, Logger);
            await dl.VersionExchange(MOBILEBACKUP2_VERSION_MAJOR, MOBILEBACKUP2_VERSION_MINOR, cancellationToken).ConfigureAwait(false);
            await VersionExchange(dl, cancellationToken).ConfigureAwait(false);
            return dl;
        }

        /// <summary>
        /// Exchange versions with the device and assert that the device supports our version of the protocol.
        /// </summary>
        /// <param name="dl">Initialized device link.</param>
        private static async Task VersionExchange(DeviceLinkService dl, CancellationToken cancellationToken)
        {
            ArrayNode supportedVersions = [
                new RealNode(2.0),
                new RealNode(2.1)
            ];
            dl.SendProcessMessage(new DictionaryNode() {
                {"MessageName", new StringNode("Hello") },
                {"SupportedProtocolVersions", supportedVersions }
            });

            ArrayNode reply = await dl.ReceiveMessage(cancellationToken);
            if (reply[0].AsStringNode().Value != "DLMessageProcessMessage" || reply[1].AsDictionaryNode()["ErrorCode"].AsIntegerNode().Value != 0) {
                throw new Mobilebackup2Exception("Found error in response during version exchange");
            }
            if (!supportedVersions.Contains(reply[1].AsDictionaryNode()["ProtocolVersion"])) {
                throw new Mobilebackup2Exception("Unsuppored protocol version found");
            }
        }

        /// <summary>
        /// Backup a device
        /// </summary>
        /// <param name="fullBackup">Whether to do a full backup; if true any previous backup attempts will be discarded</param>
        /// <param name="ignoreTransferErrors">Whether to skip over any transfer errors</param>
        /// <param name="performBackupSizeCheck">Whether to check that the size of the backup will fit onto this device</param>
        /// <param name="backupDirectory">Directory to write backup to</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<ResultCode> Backup(bool fullBackup = true, bool ignoreTransferErrors = true, bool performBackupSizeCheck = true, string backupDirectory = ".", CancellationToken cancellationToken = default)
        {
            _internalCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            string deviceDirectory = Path.Combine(backupDirectory, Lockdown.Udid);
            Directory.CreateDirectory(deviceDirectory);

            using (DeviceLinkService dl = await GetDeviceLink(backupDirectory, ignoreTransferErrors, performBackupSizeCheck, _internalCts.Token).ConfigureAwait(false)) {
                try {
                    dl.BeforeReceivingFile += DeviceLink_BeforeReceivingFile;
                    dl.Completed += DeviceLink_Completed;
                    dl.Error += DeviceLink_Error;
                    dl.FileReceived += DeviceLink_FileReceived;
                    dl.FileReceiving += DeviceLink_FileReceiving;
                    dl.FileTransferError += DeviceLink_FileTransferError;
                    dl.PasscodeRequiredForBackup += DeviceLink_PasscodeRequiredForBackup;
                    dl.Progress += DeviceLink_Progress;
                    dl.Status += DeviceLink_Status;
                    dl.Started += DeviceLink_Started;

                    using (NotificationProxyService np = new NotificationProxyService(this.Lockdown)) {
                        np.ReceivedNotification += NotificationProxy_ReceivedNotification;
                        await np.ObserveNotificationAsync(ReceivableNotification.SyncCancelRequest).ConfigureAwait(false);
                        await np.ObserveNotificationAsync(ReceivableNotification.LocalAuthenticationUiPresented)
                            .ConfigureAwait(false);
                        await np.ObserveNotificationAsync(ReceivableNotification.LocalAuthenticationUiDismissed)
                            .ConfigureAwait(false);
                        np.Start();

                        using (AfcService afc = new AfcService(this.Lockdown)) {
                            using (BackupLock backupLock = new BackupLock(afc, np)) {
                                await backupLock.AquireBackupLock(_internalCts.Token).ConfigureAwait(false);

                                // Create Info.plist
                                string infoPlistPath = Path.Combine(deviceDirectory, "Info.plist");
                                DictionaryNode infoPlist = await CreateInfoPlist(afc, _internalCts.Token).ConfigureAwait(false);
                                using (FileStream fs = File.OpenWrite(infoPlistPath)) {
                                    byte[] infoPlistData = PropertyList.SaveAsByteArray(infoPlist, PlistFormat.Xml);
                                    await fs.WriteAsync(infoPlistData, _internalCts.Token).ConfigureAwait(false);
                                    FileReceived?.Invoke(this, new BackupFileEventArgs(new BackupFile(string.Empty, infoPlistPath, deviceDirectory)));
                                }

                                // Create Manifest.plist if doesn't exist.
                                string manifestPlistPath = Path.Combine(deviceDirectory, "Manifest.plist");
                                if (fullBackup && File.Exists(manifestPlistPath)) {
                                    File.Delete(manifestPlistPath);
                                }
                                else if (!fullBackup && !File.Exists(manifestPlistPath)) {
                                    fullBackup = true;
                                }

                                // Create Status.plist file if doesn't exist.
                                string statusPlistPath = Path.Combine(deviceDirectory, "Status.plist");
                                if (fullBackup || !File.Exists(statusPlistPath)) {
                                    BackupStatus status = new BackupStatus() { IsFullBackup = fullBackup };
                                    await File.WriteAllBytesAsync(statusPlistPath, PropertyList.SaveAsByteArray(status.ToPlist(), PlistFormat.Binary), _internalCts.Token).ConfigureAwait(false);
                                }

                                DictionaryNode message = new DictionaryNode() {
                                    { "MessageName", new StringNode("Backup") },
                                    { "TargetIdentifier", new StringNode(Lockdown.Udid) }
                                };
                                dl.SendProcessMessage(message);

                                // Wait for 3 seconds to see if the device passcode is requested
                                await Task.Delay(3000, _internalCts.Token).ConfigureAwait(false);
                                while (_passcodeRequired) {
                                    // Keep waiting till the passcode has been entered
                                    await Task.Delay(3000, _internalCts.Token).ConfigureAwait(false);
                                }

                                return await dl.DlLoop(_internalCts.Token).ConfigureAwait(false);
                            }
                        }
                    }
                }
                finally {
                    DictionaryNode message1 = new DictionaryNode() {
                        { "MessageName", new StringNode("CancelBackup") },
                        { "TargetIdentifier", new StringNode(Lockdown.Udid) }
                    };
                    dl.SendProcessMessage(message1);
                }
            }
        }

        private void NotificationProxy_ReceivedNotification(object? sender, ReceivedNotificationEventArgs e)
        {
            if (e.Event == ReceivableNotification.LocalAuthenticationUiPresented) {
                // iOS versions 15.7.1 and anything 16.1 or newer will require you to input a passcode before
                // it can start a backup so we make sure to notify the user about this.
                if ((Lockdown.OsVersion >= new Version(15, 7, 1) && Lockdown.OsVersion < new Version(16, 0)) ||
                    Lockdown.OsVersion >= new Version(16, 1)) {
                    _passcodeRequired = true;
                    PasscodeRequiredForBackup?.Invoke(this, EventArgs.Empty);
                }
            }
            else if (e.Event == ReceivableNotification.LocalAuthenticationUiDismissed) {
                _passcodeRequired = false;
            }
            else if (e.Event == ReceivableNotification.SyncCancelRequest) {
                _internalCts.Cancel();
            }
        }

        /// <summary>
        /// Restore a pre existing backup to the connected device.
        /// </summary>
        /// <param name="backupDirectory">Path to the backup directory being restored</param>
        /// <param name="system">Whether to restore system files; defaults to false</param>
        /// <param name="reboot">Reboots the device when done; defaults to false</param>
        /// <param name="copy">Create a copy of the backup folder before restoring; defaults to true</param>
        /// <param name="settings">Restore device settings; defaults to true</param>
        /// <param name="remove">Remove items which aren't being restored; defaults to false</param>
        /// <param name="password">The password for the backup if it is encrypted</param>
        /// <param name="source">Identifier of device to restore it's backup</param>
        public async Task<ResultCode> Restore(string backupDirectory, bool system = false, bool reboot = false,
            bool copy = true,
            bool settings = true, bool remove = false, string password = "", string source = "",
            bool ignoreTransferErrors = false, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(source)) {
                source = Lockdown.Udid;
            }

            if (!BackupExists(backupDirectory, source)) {
                throw new Mobilebackup2Exception("Backup not found");
            }

            using (DeviceLinkService dl =
                   await GetDeviceLink(backupDirectory, ignoreTransferErrors, true, cancellationToken)
                       .ConfigureAwait(false)) {
                dl.BeforeReceivingFile += DeviceLink_BeforeReceivingFile;
                dl.Completed += DeviceLink_Completed;
                dl.Error += DeviceLink_Error;
                dl.FileReceived += DeviceLink_FileReceived;
                dl.FileReceiving += DeviceLink_FileReceiving;
                dl.FileTransferError += DeviceLink_FileTransferError;
                dl.PasscodeRequiredForBackup += DeviceLink_PasscodeRequiredForBackup;
                dl.Progress += DeviceLink_Progress;
                dl.Status += DeviceLink_Status;
                dl.Started += DeviceLink_Started;

                using (NotificationProxyService np = new NotificationProxyService(this.Lockdown)) {
                    using (AfcService afc = new AfcService(this.Lockdown)) {
                        using (BackupLock backupLock = new BackupLock(afc, np)) {
                            await backupLock.AquireBackupLock(cancellationToken).ConfigureAwait(false);

                            string manifestPlistPath = Path.Combine(backupDirectory, source, "Manifest.plist");
                            DictionaryNode manifestPlist;
                            using (FileStream fs = new FileStream(manifestPlistPath, FileMode.Open, FileAccess.Read)) {
                                PropertyNode plist = await PropertyList.LoadAsync(fs).ConfigureAwait(false);
                                manifestPlist = plist.AsDictionaryNode();
                            }

                            bool isEncrypted = false;
                            if (manifestPlist.TryGetValue("IsEncrypted", out PropertyNode? isEncryptedNode)) {
                                isEncrypted = isEncryptedNode.AsBooleanNode().Value;
                            }

                            DictionaryNode options = new DictionaryNode() {
                                { "RestoreShouldReboot", new BooleanNode(reboot) },
                                { "RestoreDontCopyBackup", new BooleanNode(!copy) },
                                { "RestorePreserveSettings", new BooleanNode(settings) },
                                { "RestoreSystemFiles", new BooleanNode(system) },
                                { "RemoveItemsNotRestored", new BooleanNode(remove) }
                            };

                            if (isEncrypted) {
                                if (string.IsNullOrEmpty(password)) {
                                    options.Add("Password", new StringNode(password));
                                }
                                else {
                                    Logger.LogError("Backup is encrypted, but no password is supplied");
                                    throw new Mobilebackup2Exception("Password missing from encrypted backup restore");
                                }
                            }

                            DictionaryNode message = new DictionaryNode() {
                                { "MessageName", new StringNode("Restore") },
                                { "TargetIdentifier", new StringNode(Lockdown.Udid) },
                                { "SourceIdentifier", new StringNode(source) },
                                { "Options", options },
                            };
                            dl.SendProcessMessage(message);

                            return await dl.DlLoop(cancellationToken).ConfigureAwait(false);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="currentPassword"></param>
        /// <param name="newPassword"></param>
        public async Task<ResultCode> ChangeBackupPassword(string currentPassword, string newPassword,
            CancellationToken cancellationToken = default)
        {
            return await ChangeBackupEncryptionPassword(currentPassword, newPassword,
                BackupEncryptionFlags.ChangePassword, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Enables encrypted backups by setting a password for backups to use provided
        /// the phone currently has encrypted backups disabled. 
        /// </summary>
        /// <param name="password">The password to set for backup encryption</param>
        public async Task<ResultCode> SetBackupPassword(string password, CancellationToken cancellationToken = default)
        {
            return await ChangeBackupEncryptionPassword(null, password, BackupEncryptionFlags.Enable, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Disables encrypted backups on the device by removing the password for backups
        /// </summary>
        /// <param name="currentPassword">The current password for the enabled backup encryption</param>
        public async Task<ResultCode> RemoveBackupPassword(string currentPassword,
            CancellationToken cancellationToken = default)
        {
            return await ChangeBackupEncryptionPassword(currentPassword, null, BackupEncryptionFlags.Disable,
                cancellationToken).ConfigureAwait(false);
        }
    }
}

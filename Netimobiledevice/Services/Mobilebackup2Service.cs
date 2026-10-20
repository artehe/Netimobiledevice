using Microsoft.Extensions.Logging;
using Netimobiledevice.Lockdown;
using Netimobiledevice.NotificationProxy;
using Netimobiledevice.Plist;
using Netimobiledevice.Services.Afc;
using Netimobiledevice.Services.Mobilebackup2;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Services;

/// <summary>
/// Client for the Mobilebackup2 service, the iTunes/Finder-style device backup protocol.
/// <para>
/// Drives full and incremental backups, restores, and the related operations (info, list,
/// extract, unback, change password, erase device) over a <see cref="DeviceLink"/> channel. Backups can
/// be filtered to a subset of files via a filter callback, and encrypted backups are
/// supported (a password is required when patching an encrypted manifest or restoring an
/// encrypted backup). The right underlying service is selected automatically: <see cref="LockdownServiceName"/>
/// over classic lockdown, or <see cref="RemoteServiceName"/> over RemoteXPC/RSD.
/// </para>
/// </summary>
public sealed class Mobilebackup2Service(
    LockdownServiceProvider lockdown,
    ILogger? logger = null
) : LockdownService(
    lockdown,
    lockdown is LockdownClient ? LockdownServiceName : RemoteServiceName,
    includeEscrowBag: true,
    logger: logger
) {
    private const string InfoPlist = "Info.plist";
    private const string ManifestDatabase = "Manifest.db";
    private const string ManifestPlist = "Manifest.plist";
    private const string StatusPlist = "Status.plist";

    public const string LockdownServiceName = "com.apple.mobilebackup2";
    public const string RemoteServiceName = "com.apple.mobilebackup2.shim.remote";

    private static readonly HashSet<string> BackupMetadataFiles = [
        InfoPlist,
        ManifestPlist,
        ManifestDatabase,
        "Manifest.db-shm",
        "Manifest.db-wal",
        StatusPlist,
    ];
    private static readonly string[] BackupObservedNotifications = [
        ReceivableNotification.SyncCancelRequest,
        ReceivableNotification.SyncSuspendRequst,
        ReceivableNotification.SyncResumeRequst,
        ReceivableNotification.BackupDomainChanged,
        ReceivableNotification.LocalAuthenticationUiDismissed,
        ReceivableNotification.LocalAuthenticationUiDismissed
    ];
    private static readonly string[] IncrementalBackupRequiredFiles = [
        ManifestPlist,
        ManifestDatabase,
        StatusPlist,
    ];
    /// <summary>
    /// iTunes files to be inserted into the Info.plist file.
    /// </summary>
    private static readonly string[] ITunesFiles = [
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
        "iTunesPrefs.plist",
    ];
    private static readonly IReadOnlyList<double> SupportedVersions = new[] { 2.0, 2.1 };

    private static void AssertBackupExists(string backupDirectory, string identifier) {
        string deviceDirectory = Path.Combine(backupDirectory, identifier);
        if (!File.Exists(Path.Combine(deviceDirectory, InfoPlist)) ||
            !File.Exists(Path.Combine(deviceDirectory, ManifestPlist)) ||
            !File.Exists(Path.Combine(deviceDirectory, StatusPlist))
        ) {
            throw new Mobilebackup2Exception($"No valid backup found for {identifier} in {backupDirectory}");
        }
    }

    private DictionaryNode CreateMessage(string messageName) => new DictionaryNode {
        ["MessageName"] = new StringNode(messageName),
        ["TargetIdentifier"] = new StringNode(Lockdown.Udid),
    };

    /// <summary>
    /// Create a connected <see cref="DeviceLink"/> for backup operations.
    /// <para>
    /// Performs the device-link and mobilebackup2 version exchanges on entry. Dispose the returned
    /// link to disconnect. The given filter callback governs which incoming files are written to disk.
    /// </para>
    /// </summary>
    /// <param name="backupDirectory">Directory the device link reads from and writes to.</param>
    /// <param name="filterCallback">Optional predicate controlling which files are preserved.</param>
    private async Task<DeviceLink> DeviceLinkAsync(
            string backupDirectory,
            BackupFilterCallback? filterCallback = null,
            CancellationToken ct = default
        ) {
        await ConnectAsync(ct).ConfigureAwait(false);
        if (Service is null) {
            throw new Mobilebackup2Exception("mobilebackup2 service failed to connect");
        }

        DeviceLink dl = new DeviceLink(
            Service,
            backupDirectory,
            preserveFile: (fileName, deviceName) => ShouldPreserveBackupFile(fileName, deviceName, filterCallback)
        );
        await dl.VersionExchangeAsync(400, 0, ct).ConfigureAwait(false);
        await VersionExchangeAsync(dl, cancellationToken: ct).ConfigureAwait(false);

        return dl;
    }

    private static bool HasIncrementalBackupMetadata(
        string deviceDirectory
    ) => IncrementalBackupRequiredFiles.All(filename => {
        string path = Path.Combine(deviceDirectory, filename);
        return File.Exists(path) && new FileInfo(path).Length > 0;
    });

    /// <summary>
    /// Build the Info.plist dictionary describing the device for a new backup.
    /// <para>
    /// Collects device identity values, the list of installed user apps (with their SINF and
    /// iTunes metadata where available), and the iTunes control files needed to make the
    /// backup restorable.
    /// </para>
    /// </summary>
    /// <param name="afc">An open AFC service used to read the device's iTunes control files.</param>
    /// <returns>The assembled Info.plist contents as a dictionary.</returns>
    private async Task<DictionaryNode> InitInfoPlistAsync(AfcService afc, CancellationToken ct) {
        await using (InstallationProxyService ip = new InstallationProxyService(Lockdown)) {
            await using (SpringBoardServicesService sbs = new SpringBoardServicesService(Lockdown)) {
                PropertyNode? rootNode = await Lockdown.GetValueAsync();
                DictionaryNode root = rootNode?.AsDictionaryNode() ?? [];

                DictionaryNode? itunesSettings = root.TryGetValue("com.apple.iTunes", out PropertyNode? itn)
                    ? itn.AsDictionaryNode()
                    : null;

                PropertyNode? minITunesVersion = null;
                if (root.TryGetValue("com.apple.mobile.iTunes", out PropertyNode? mobileItunesObj) &&
                    mobileItunesObj is DictionaryNode mobileItunes &&
                    mobileItunes.TryGetValue("MinITunesVersion", out PropertyNode? minVersionObj)
                ) {
                    minITunesVersion = minVersionObj;
                }

                DictionaryNode appDict = [];
                ArrayNode installedApps = [];

                await ip.ConnectAsync(ct);
                await sbs.ConnectAsync(ct);

                ArrayNode apps = await ip.BrowseAsync(
                    options: new DictionaryNode {
                        ["ApplicationType"] = new StringNode("User")
                    },
                    attributes: [
                        new StringNode("CFBundleIdentifier"),
                        new StringNode("ApplicationSINF"),
                        new StringNode("iTunesMetadata")
                    ],
                    ct
                ).ConfigureAwait(false);
                foreach (PropertyNode appNode in apps) {
                    if (appNode is not DictionaryNode app) {
                        continue;
                    }

                    if (app.TryGetValue("CFBundleIdentifier", out PropertyNode? bundleId)) {
                        installedApps.Add(bundleId);

                        if (app.TryGetValue("iTunesMetadata", out PropertyNode? metadata) &&
                            app.TryGetValue("ApplicationSINF", out PropertyNode? sinf)
                        ) {
                            string bid = bundleId.AsStringNode().Value;

                            appDict[bid] = new DictionaryNode {
                                ["ApplicationSINF"] = sinf,
                                ["iTunesMetadata"] = metadata,
                                ["PlaceholderIcon"] = await sbs.GetIconPngDataAsync(bid, ct).ConfigureAwait(false),
                            };
                        }
                    }
                }

                DictionaryNode files = [];
                foreach (string file in ITunesFiles) {
                    string filePath = $"/iTunes_Control/iTunes/{file}";
                    try {
                        byte[] dataBuf = await afc.GetFileContentsAsync(filePath, ct).ConfigureAwait(false);
                        files[file] = new DataNode(dataBuf);
                    }
                    catch (AfcFileNotFoundException) {
                        // Missing file so we skip it.
                    }
                }

                DictionaryNode ret = new DictionaryNode {
                    ["iTunes Version"] = minITunesVersion ?? new StringNode("10.0.1"),
                    ["iTunes Files"] = files,
                    ["Unique Identifier"] = new StringNode(Lockdown.Udid.ToUpperInvariant()),
                    ["Target Type"] = new StringNode("Device"),
                    ["Target Identifier"] = root["UniqueDeviceID"],
                    ["Serial Number"] = root["SerialNumber"],
                    ["Product Version"] = root["ProductVersion"],
                    ["Product Type"] = root["ProductType"],
                    ["Installed Applications"] = installedApps,
                    ["GUID"] = new StringNode(Guid.NewGuid().ToString()),
                    ["Display Name"] = root["DeviceName"],
                    ["Device Name"] = root["DeviceName"],
                    ["Build Version"] = root["BuildVersion"],
                    ["Applications"] = appDict,
                    ["Last Backup Date"] = new DateNode(DateTime.Now)
                };

                if (root.TryGetValue("IntegratedCircuitCardIdentity", out PropertyNode? iccid)) {
                    ret["ICCID"] = iccid;
                }
                if (root.TryGetValue("InternationalMobileEquipmentIdentity", out PropertyNode? imei)) {
                    ret["IMEI"] = imei;
                }
                if (root.TryGetValue("MobileEquipmentIdentifier", out PropertyNode? meid)) {
                    ret["MEID"] = meid;
                }
                if (root.TryGetValue("PhoneNumber", out PropertyNode? phoneNumber)) {
                    ret["Phone Number"] = phoneNumber;
                }

                try {
                    byte[] dataBuf = await afc.GetFileContentsAsync("/Books/iBooksData2.plist", ct).ConfigureAwait(false);
                    ret["iBooks Data 2"] = new DataNode(dataBuf);
                }
                catch (AfcFileNotFoundException) {
                    // Optional iBooks data not present.
                }

                if (itunesSettings is not null && itunesSettings.Count > 0) {
                    ret["iTunes Settings"] = itunesSettings;
                }

                return ret;
            }
        }
    }

    private void LogBackupNotification(DictionaryNode notification) {
        if (notification.TryGetValue("Name", out PropertyNode? notificationNameNode)) {
            string notificationName = notificationNameNode.AsStringNode().Value;
            if (Logger.IsEnabled(LogLevel.Debug)) {
                Logger.LogDebug("Got notification {notificationName}", notificationName);
            }

            switch (notificationName) {
                case ReceivableNotification.LocalAuthenticationUiPresented: {
                    Logger.LogWarning("Please enter the device passcode to continue the backup");
                    break;
                }

                case ReceivableNotification.LocalAuthenticationUiDismissed: {
                    Logger.LogInformation("Device passcode prompt dismissed");
                    break;
                }

                case ReceivableNotification.SyncCancelRequest: {
                    Logger.LogWarning("User has cancelled the backup process on the device");
                    break;
                }

                default: {
                    if (Logger.IsEnabled(LogLevel.Debug)) {
                        Logger.LogDebug("Received backup notification: {notification}", notification);
                    }
                    break;

                }
            }
        }
    }

    private async Task LogBackupNotificationsAsync(
        NotificationProxyService notificationProxy,
        CancellationToken cancellationToken
    ) {
        await foreach (DictionaryNode? notification in notificationProxy.ReceiveNotificationAsync(cancellationToken).ConfigureAwait(false)) {
            LogBackupNotification(notification ?? []);
        }
    }

    private static async Task ObserveBackupNotificationsAsync(NotificationProxyService notificationProxy, CancellationToken ct) {
        foreach (string notification in BackupObservedNotifications) {
            await notificationProxy.NotifyRegisterDispatchAsync(notification, ct).ConfigureAwait(false);
        }
    }

    private static bool ShouldDoFullBackup(
        bool full,
        string deviceDirectory,
        bool patchManifest = false
    ) => full || patchManifest || !HasIncrementalBackupMetadata(deviceDirectory);

    /// <summary>
    /// Decide whether a backup file should be preserved.
    /// <para>
    /// Known backup metadata files (<see cref="BackupMetadataFiles"/>) are always preserved. With no
    /// filter callback every file is preserved; otherwise the callback decides.
    /// </para>
    /// </summary>
    /// <param name="fileName">The file name (with or without path) to evaluate.</param>
    /// <param name="deviceName">The device-side name associated with the backup file.</param>
    /// <param name="filterCallback">
    /// Optional predicate taking a <see cref="BackupFile"/> and returning whether to preserve it.
    /// </param>
    /// <returns><see langword="true"/> if the file should be preserved, <see langword="false"/> otherwise.</returns>
    private static bool ShouldPreserveBackupFile(
        string fileName,
        string deviceName,
        BackupFilterCallback? filterCallback
    ) {
        if (BackupMetadataFiles.Contains(Path.GetFileName(fileName))) {
            return true;
        }

        if (filterCallback is null) {
            return true;
        }

        return filterCallback(
            new BackupFile(
                FileName: fileName,
                DeviceName: deviceName
            )
        );
    }

    /// <summary>
    /// Exchange protocol versions with the device and assert it supports one of ours.
    /// </summary>
    /// <param name="dl">An initialized device link channel.</param>
    /// <param name="localVersions">
    /// Protocol versions supported by the host; defaults to <see cref="SupportedVersions"/>.
    /// </param>
    private static async Task VersionExchangeAsync(
        DeviceLink dl,
        ArrayNode? localVersions = null,
        CancellationToken cancellationToken = default
    ) {
        localVersions ??= [
            new RealNode(2.0),
            new RealNode(2.1)
        ];
        await dl.SendProcessMessageAsync(
            new DictionaryNode() {
                ["MessageName"] = new StringNode("Hello"),
                ["SupportedProtocolVersions"] = localVersions,
            },
            cancellationToken
        ).ConfigureAwait(false);

        ArrayNode reply = await dl.ReceiveMessageAsync(cancellationToken).ConfigureAwait(false);

        string header = reply[0].AsStringNode().Value ?? string.Empty;
        DictionaryNode body = reply[1].AsDictionaryNode() ?? [];

        if (header != DeviceLinkMessage.ProcessMessage || body["ErrorCode"].AsIntegerNode().Value != 0) {
            throw new Mobilebackup2Exception("Unexpected reply to version exchange");
        }

        RealNode protocolVersion = body["ProtocolVersion"].AsRealNode();
        if (!localVersions.Contains(protocolVersion)) {
            throw new Mobilebackup2Exception($"Device protocol version {protocolVersion} is unsupported");
        }
    }

    /// <summary>
    /// Back up the device into <c><paramref name="backupDirectory"/>/&lt;device-udid&gt;</c>.
    /// </summary>
    /// <param name="fullBackup">
    /// Perform a full backup, discarding any previous incremental state. A full backup is also
    /// forced when incremental metadata is missing.
    /// </param>
    /// <param name="backupDirectory">
    /// Directory the backup is written to (a per-device subdirectory is created under it).
    /// </param>
    /// <param name="progressCallback">
    /// Called as the backup progresses with the completion percentage as its sole argument.
    /// </param>
    /// <param name="filterCallback">
    /// Optional predicate deciding which backup payloads to keep. The rejected payload bytes are
    /// drained but not stored.
    /// </param>
    /// <param name="password">Backup password, used when patching an encrypted backup.</param>
    /// <param name="patchManifest">
    /// Remove entries for rejected payloads from Manifest.db. This forces a full backup and requires
    /// the password when backup encryption is enabled.
    /// </param>
    /// <exception cref="BackupFilterPasswordRequiredException">
    /// If manifest patching is requested without a password while the device encrypts backups.
    /// </exception>
    public async Task BackupAsync(
        bool fullBackup = true,
        string backupDirectory = ".",
        ProgressCallback? progressCallback = null,
        BackupFilterCallback? filterCallback = null,
        string password = "",
        bool patchManifest = false,
        CancellationToken cancellationToken = default
    ) {
        progressCallback ??= _ => { };

        string deviceDirectory = Path.Combine(backupDirectory, Lockdown.Udid);
        Directory.CreateDirectory(deviceDirectory);

        patchManifest = patchManifest && filterCallback is not null;
        fullBackup = ShouldDoFullBackup(fullBackup, deviceDirectory, patchManifest);

        if (patchManifest &&
            string.IsNullOrEmpty(password) &&
            await GetWillEncryptAsync().ConfigureAwait(false)
        ) {
            throw new BackupFilterPasswordRequiredException("Patching an encrypted backup manifest requires the backup password");
        }

        await using (DeviceLink dl = await DeviceLinkAsync(backupDirectory, filterCallback, cancellationToken).ConfigureAwait(false)) {
            await using (NotificationProxyService notificationProxy = new NotificationProxyService(Lockdown)) {
                await notificationProxy.ConnectAsync(cancellationToken);

                await using (AfcService afc = new AfcService(Lockdown)) {
                    await afc.ConnectAsync(cancellationToken);

                    await using (BackupLock backupLock = await BackupLock.Create(afc, notificationProxy, cancellationToken).ConfigureAwait(false)) {
                        await ObserveBackupNotificationsAsync(notificationProxy, cancellationToken).ConfigureAwait(false);

                        using (CancellationTokenSource notificationCts = new CancellationTokenSource()) {
                            Task notificationTask = LogBackupNotificationsAsync(notificationProxy, notificationCts.Token);

                            try {
                                // Initialize Info.plist
                                DictionaryNode infoPlist = await InitInfoPlistAsync(afc, cancellationToken).ConfigureAwait(false);
                                await using (FileStream fd = File.Create(Path.Combine(deviceDirectory, InfoPlist))) {
                                    await PropertyList.SaveAsync(infoPlist, fd, PlistFormat.Xml);
                                }

                                // Create Manifest.plist if doesn't exist.
                                string manifestPlistPath = Path.Combine(deviceDirectory, ManifestPlist);
                                if (fullBackup && File.Exists(manifestPlistPath)) {
                                    File.Delete(manifestPlistPath);
                                }
                                else if (!File.Exists(manifestPlistPath)) {
                                    fullBackup = true;
                                }
                                else {
                                    File.SetLastWriteTimeUtc(manifestPlistPath, DateTime.UtcNow);
                                }

                                // Initialize Status.plist file if doesn't exist.
                                string statusPlistPath = Path.Combine(deviceDirectory, StatusPlist);
                                if (fullBackup || !File.Exists(statusPlistPath)) {
                                    BackupStatus status = new BackupStatus() {
                                        IsFullBackup = fullBackup
                                    };
                                    await using (FileStream fd = File.Create(statusPlistPath)) {
                                        await PropertyList.SaveAsync(status.ToPlist(), fd, PlistFormat.Binary);
                                    }
                                }

                                await dl.SendProcessMessageAsync(
                                    CreateMessage("Backup"),
                                    cancellationToken
                                ).ConfigureAwait(false);

                                try {
                                    await dl.DlLoopAsync(progressCallback, cancellationToken).ConfigureAwait(false);
                                }
                                finally {
                                    dl.CleanupDiscardedFiles();
                                }
                            }
                            finally {
                                notificationCts.Cancel();
                                try {
                                    await notificationTask.ConfigureAwait(false);
                                }
                                catch (Exception ex) when (ex is OperationCanceledException or TimeoutException) {
                                    // Expected on cancellation.
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Change, enable, or disable the device's backup encryption password.
    /// </summary>
    /// <param name="backupDirectory">Path of the backup directory.</param>
    /// <param name="old">Previous password. Omit when enabling backup encryption.</param>
    /// <param name="new">New password. Omit when disabling backup encryption.</param>
    public async Task ChangePasswordAsync(
        string backupDirectory = ".",
        string oldPassword = "",
        string newPassword = "",
        CancellationToken cancellationToken = default
    ) {
        await using (DeviceLink dl = await DeviceLinkAsync(backupDirectory, ct: cancellationToken).ConfigureAwait(false)) {
            DictionaryNode message = CreateMessage("ChangePassword");
            if (!string.IsNullOrEmpty(oldPassword)) {
                message["OldPassword"] = new StringNode(oldPassword);
            }

            if (!string.IsNullOrEmpty(newPassword)) {
                message["NewPassword"] = new StringNode(newPassword);
            }

            await dl.SendProcessMessageAsync(message, cancellationToken).ConfigureAwait(false);
            await dl.DlLoopAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Erase the device, restoring it to factory state.
    /// </summary>
    /// <param name="backupDirectory">Path of the backup directory used for the device link channel.</param>
    public async Task EraseDeviceAsync(
        string backupDirectory = ".",
        CancellationToken cancellationToken = default
    ) {
        try {
            await using (DeviceLink dl = await DeviceLinkAsync(backupDirectory, ct: cancellationToken).ConfigureAwait(false)) {
                await dl.SendProcessMessageAsync(
                    CreateMessage("EraseDevice"),
                    cancellationToken
                ).ConfigureAwait(false);
                await dl.DlLoopAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }
        catch (EndOfStreamException) {
            // Device may drop the connection as it erases;
        }
    }

    /// <summary>
    /// Report whether the device is configured to encrypt its backups.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if backup encryption is enabled on the device, <see langword="false"/> otherwise
    /// (including when the value cannot be read).
    /// </returns>
    public async Task<bool> GetWillEncryptAsync() {
        try {
            PropertyNode? willEncrypt = await Lockdown.GetValueAsync("com.apple.mobile.backup", "WillEncrypt").ConfigureAwait(false);
            return willEncrypt?.AsBooleanNode().Value ?? false;
        }
        catch (LockdownException) {
            return false;
        }
    }

    /// <summary>
    /// Restore a previously created backup to the device.
    /// </summary>
    /// <param name="backupDirectory">Path of the backup directory.</param>
    /// <param name="system">Whether to restore system files.</param>
    /// <param name="reboot">Reboot the device when done.</param>
    /// <param name="copy">Create a copy of the backup folder before restoring.</param>
    /// <param name="settings">Restore device settings.</param>
    /// <param name="remove">Remove items on the device that aren't part of the restore.</param>
    /// <param name="password">Password of the backup; required if the backup is encrypted.</param>
    /// <param name="source">
    /// Identifier of the device whose backup is restored; defaults to the connected device's UDID.
    /// </param>
    /// <param name="progressCallback">
    /// Called as the restore progresses with the completion percentage as its sole argument.
    /// </param>
    /// <param name="skipApps">Do not trigger re-installation of apps after the restore.</param>
    public async Task RestoreAsync(
        string backupDirectory = ".",
        bool system = false,
        bool reboot = true,
        bool copy = true,
        bool settings = true,
        bool remove = false,
        string password = "",
        string source = "",
        ProgressCallback? progressCallback = null,
        bool skipApps = false,
        CancellationToken cancellationToken = default
    ) {
        progressCallback ??= _ => { };

        source = string.IsNullOrEmpty(source) ? Lockdown.Udid : source;
        AssertBackupExists(backupDirectory, source);

        await using (DeviceLink dl = await DeviceLinkAsync(backupDirectory, ct: cancellationToken).ConfigureAwait(false)) {
            await using (NotificationProxyService notificationProxy = new NotificationProxyService(Lockdown)) {
                await notificationProxy.ConnectAsync(cancellationToken);

                await using (AfcService afc = new AfcService(Lockdown)) {
                    await afc.ConnectAsync(cancellationToken);

                    await using (BackupLock backupLock = await BackupLock.Create(afc, notificationProxy, cancellationToken).ConfigureAwait(false)) {
                        string manifestPlistPath = Path.Combine(backupDirectory, source, ManifestPlist);
                        byte[] manifestPlistFile = await File.ReadAllBytesAsync(manifestPlistPath, cancellationToken);
                        DictionaryNode manifest = PropertyList.LoadFromByteArray(manifestPlistFile).AsDictionaryNode();

                        bool isEncrypted = manifest.TryGetValue("IsEncrypted", out PropertyNode? enc) && enc.AsBooleanNode().Value;
                        DictionaryNode options = new DictionaryNode {
                            ["RestoreShouldReboot"] = new BooleanNode(reboot),
                            ["RestoreDontCopyBackup"] = new BooleanNode(!copy),
                            ["RestorePreserveSettings"] = new BooleanNode(settings),
                            ["RestoreSystemFiles"] = new BooleanNode(system),
                            ["RemoveItemsNotRestored"] = new BooleanNode(remove),
                        };
                        if (isEncrypted) {
                            if (!string.IsNullOrEmpty(password)) {
                                options["Password"] = new StringNode(password);
                            }
                            else {
                                Logger.LogError("Backup is encrypted, please supply password.");
                                return;
                            }
                        }

                        DictionaryNode message = CreateMessage("Restore");
                        message.Add("SourceIdentifier", new StringNode(source));
                        message.Add("Options", options);

                        await dl.SendProcessMessageAsync(message, cancellationToken).ConfigureAwait(false);

                        if (!skipApps) {
                            // Write /iTunesRestore/RestoreApplications.plist so that the device will start
                            // restoring applications once the rest of the restore process is finished
                            string infoPlistPath = Path.Combine(backupDirectory, source, "Info.plist");
                            byte[] infoPlistFile = await File.ReadAllBytesAsync(infoPlistPath, cancellationToken);
                            DictionaryNode infoPlist = PropertyList.LoadFromByteArray(infoPlistFile).AsDictionaryNode();

                            if (infoPlist.TryGetValue("Applications", out PropertyNode? applicationsNode) && applicationsNode is not null) {
                                DictionaryNode applications = applicationsNode.AsDictionaryNode();

                                await afc.MakeDirsAsync("/iTunesRestore", cancellationToken).ConfigureAwait(false);
                                await afc.SetFileContentsAsync(
                                    "/iTunesRestore/RestoreApplications.plist",
                                    PropertyList.SaveAsByteArray(applications, PlistFormat.Xml),
                                    cancellationToken
                                ).ConfigureAwait(false);
                            }
                        }

                        await dl.DlLoopAsync(progressCallback, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }
    }
}

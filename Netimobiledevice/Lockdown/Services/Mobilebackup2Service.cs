using Netimobiledevice.Exceptions;
using Netimobiledevice.Plist;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Netimobiledevice.Lockdown.Services
{
    public sealed class Mobilebackup2Service : BaseService
    {
        private static readonly string[] itunesFiles = new string[] {
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

        protected override string ServiceName => "com.apple.mobilebackup2";

        public Mobilebackup2Service(LockdownClient client) : base(client) { }

        private DeviceBackupLock GetDeviceBackupLock(AfcService afcService, NotificationProxyService notificationProxyService)
        {
            DeviceBackupLock deviceBackupLock = new DeviceBackupLock(afcService, notificationProxyService);
            deviceBackupLock.AquireLock();
            return deviceBackupLock;
        }

        private async Task<DeviceLink> GetDeviceLink(string? backupDirectory = null)
        {
            var deviceLink = new DeviceLink(Service, backupDirectory);
            await deviceLink.VersionExchange();
            await VersionExchange(deviceLink);
            return deviceLink;
        }

        private async Task<PropertyNode> GenerateInfoPlist(AfcService afcService)
        {
            InstallationProxyService installationProxyService = new InstallationProxyService(Lockdown);
            SpringBoardServicesService springBoardServicesService = new SpringBoardServicesService(Lockdown);

            DictionaryNode rootNode = Lockdown.GetValue().AsDictionaryNode();
            PropertyNode itunesSettings = Lockdown.GetValue("com.apple.iTunes", null);
            PropertyNode minItunesVersion = Lockdown.GetValue("com.apple.mobile.iTunes", "MinITunesVersion");

            DictionaryNode appDict = new DictionaryNode();
            ArrayNode installedApps = new ArrayNode();

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

            DictionaryNode files = new DictionaryNode();
            foreach (string file in itunesFiles) {
                try {
                    string filePath = Path.Combine("/iTunes_Control/iTunes", file);
                    byte[] dataBuffer = afcService.GetFileContents(filePath);
                    files.Add(file, new DataNode(dataBuffer));
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

            DictionaryNode infoPlist = new DictionaryNode {
                { "iTunes Version", minItunesVersion ?? new StringNode("10.0.1") },
                { "iTunes Files", files },
                { "Unique Identifier", new StringNode(Lockdown.UDID.ToUpper()) },
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
                { "Applications", appDict }
            };

            if (rootNode.ContainsKey("IntegratedCircuitCardIdentity")) {
                infoPlist.Add("ICCID", rootNode["IntegratedCircuitCardIdentity"]);
            }
            if (rootNode.ContainsKey("InternationalMobileEquipmentIdentity")) {
                infoPlist.Add("IMEI", rootNode["InternationalMobileEquipmentIdentity"]);
            }
            if (rootNode.ContainsKey("MobileEquipmentIdentifier")) {
                infoPlist.Add("MEID", rootNode["MobileEquipmentIdentifier"]);
            }
            if (rootNode.ContainsKey("PhoneNumber")) {
                infoPlist.Add("Phone Number", rootNode["PhoneNumber"]);
            }

            try {
                byte[] dataBuffer = afcService.GetFileContents("/Books/iBooksData2.plist");
                infoPlist.Add("iBooks Data 2", new DataNode(dataBuffer));
            }
            catch (AfcException ex) {
                if (ex.AfcError != AfcError.ObjectNotFound) {
                    throw;
                }
            }

            if (itunesSettings != null) {
                infoPlist.Add("iTunes Settings", itunesSettings);
            }

            return infoPlist;
        }

        /// <summary>
        /// Exchange versions with the device and assert that the device supports our version of the protocol.
        /// </summary>
        /// <param name="deviceLink">Initialized device link.</param>
        private static async Task VersionExchange(DeviceLink deviceLink)
        {
            ArrayNode supportedVersions = new ArrayNode {
                new RealNode(2.0),
                new RealNode(2.1)
            };
            deviceLink.SendProcessMessage(new DictionaryNode() {
                {"MessageName", new StringNode("Hello") },
                {"SupportedProtocolVersions", supportedVersions }
            });

            ArrayNode reply = await deviceLink.ReceiveMessage();
            if (reply[0].AsStringNode().Value != "DLMessageProcessMessage" || reply[1].AsDictionaryNode()["ErrorCode"].AsIntegerNode().Value != 0) {
                throw new Exception($"Found error in response during version exchange");
            }
            if (!supportedVersions.Contains(reply[1].AsDictionaryNode()["ProtocolVersion"])) {
                throw new Exception("Unsuppored protocol version found");
            }
        }

        /// <summary>
        /// Backup a device.
        /// </summary>
        /// <param name="fullBackup">Whether to do a full backup. If full is True, any previous backup attempts will be discarded.</param>
        /// <param name="backupDirectory">Directory to write backup to.</param>
        /// <param name="progressCallback">Function to be called as the backup progresses.</param>
        public async Task Backup(bool fullBackup = true, string backupDirectory = ".", Action<PropertyNode>? progressCallback = null)
        {
            string deviceDirectory = Path.Combine(backupDirectory, Lockdown.UDID);
            Directory.CreateDirectory(deviceDirectory);

            using (DeviceLink deviceLink = await GetDeviceLink(backupDirectory)) {
                NotificationProxyService notificationProxyService = new NotificationProxyService(Lockdown);
                AfcService afcService = new AfcService(Lockdown);

                using (DeviceBackupLock backupLock = GetDeviceBackupLock(afcService, notificationProxyService)) {
                    // Initialize Info.plist
                    PropertyNode infoPlist = await GenerateInfoPlist(afcService);
                    string infoPlistPath = Path.Combine(deviceDirectory, "Info.plist");
                    await File.WriteAllBytesAsync(infoPlistPath, PropertyList.SaveAsByteArray(infoPlist, PlistFormat.Xml));

                    // Create Manifest.plist if doesn't exist.
                    string manifestPlistPath = Path.Combine(deviceDirectory, "Manifest.plist");
                    if (fullBackup) {
                        File.Delete(manifestPlistPath);
                    }
                    File.Create(manifestPlistPath);

                    DictionaryNode backupRequest = new DictionaryNode() {
                        { "MessageName", new StringNode("Backup") },
                        { "TargetIdentifier", new StringNode(Lockdown.UDID) }
                    };
                    deviceLink.SendProcessMessage(backupRequest);

                    await deviceLink.MessageLoop(progressCallback);
                }
            }
        }
    }
}

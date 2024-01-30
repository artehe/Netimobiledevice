using Netimobiledevice.Lockdown;
using Netimobiledevice.Lockdown.Services;
using Netimobiledevice.Plist;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Netimobiledevice.Diagnostics
{
    /// <summary>
    /// Provides a service to query MobileGestalt & IORegistry keys, as well functionality to
    /// reboot, shutdown, or put the device into sleep mode.
    /// </summary>
    public sealed class DiagnosticsService : BaseService
    {
        private const string SERVICE_NAME_NEW = "com.apple.mobile.diagnostics_relay";
        private const string SERVICE_NAME_OLD = "com.apple.iosdiagnostics.relay";

        private readonly string[] _mobileGestaltKeys = new string[] {
            "AllDeviceCapabilities",
            "AllowYouTube",
            "AllowYouTubePlugin",
            "ApNonce",
            "AppleInternalInstallCapability",
            "BasebandBoardSnum",
            "BasebandCertId",
            "BasebandChipId",
            "BasebandFirmwareManifestData",
            "BasebandFirmwareVersion",
            "BasebandKeyHashInformation",
            "BasebandRegionSKU",
            "BasebandSerialNumber",
            "BasebandSkeyId",
            "BluetoothAddress",
            "BuildVersion",
            "CarrierBundleInfoArray",
            "CarrierInstallCapability",
            "cellular-data",
            "ChipID",
            "CompassCalibration",
            "contains-cellular-radio",
            "CPUArchitecture",
            "DeviceName",
            "DieId",
            "DeviceClass",
            "DeviceColor",
            "DiagData",
            "DiskUsage",
            "encrypted-data-partition",
            "EthernetMacAddress",
            "FirmwareVersion",
            "green-tea",
            "HardwarePlatform",
            "HasAllFeaturesCapability",
            "HasBaseband",
            "HWModelStr",
            "InternalBuild",
            "InternationalMobileEquipmentIdentity",
            "InverseDeviceID",
            "IsSimulator",
            "IsThereEnoughBatteryLevelForSoftwareUpdate",
            "IsUIBuild",
            "MLBSerialNumber",
            "MobileEquipmentIdentifier",
            "ModelNumber",
            "not-green-tea",
            "PartitionType",
            "ProductType",
            "ProductVersion",
            "ProximitySensorCalibration",
            "RegionalBehaviorAll",
            "RegionalBehaviorChinaBrick",
            "RegionalBehaviorGoogleMail",
            "RegionalBehaviorNoVOIP",
            "RegionalBehaviorNoWiFi",
            "RegionalBehaviorNTSC",
            "RegionalBehaviorShutterClick",
            "RegionalBehaviorVolumeLimit",
            "RegionCode",
            "RegionInfo",
            "ReleaseType",
            "RequiredBatteryLevelForSoftwareUpdate",
            "SBAllowSensitiveUI",
            "SBCanForceDebuggingInfo",
            "ScreenDimensions",
            "SDIOManufacturerTuple",
            "SDIOProductInfo",
            "SerialNumber",
            "SigningFuse",
            "SIMTrayStatus",
            "SoftwareBehavior",
            "SoftwareBundleVersion",
            "SupportedDeviceFamilies",
            "SupportedKeyboards",
            "SysCfg",
            "UniqueChipID",
            "UserAssignedDeviceName",
            "UniqueDeviceID",
            "wi-fi",
            "WifiAddress",
            "WirelessBoardSnum"
        };

        protected override string ServiceName => SERVICE_NAME_NEW;

        public DiagnosticsService(LockdownClient client) : base(client, GetDiagnosticsServiceConnection(client)) { }

        private static ServiceConnection GetDiagnosticsServiceConnection(LockdownClient client)
        {
            ServiceConnection service;
            try {
                service = client.StartService(SERVICE_NAME_NEW);
            }
            catch (Exception ex) {
                Debug.WriteLine(ex);
                service = client.StartService(SERVICE_NAME_OLD);
            }

            return service;
        }

        private PropertyNode ExecuteCommand(StringNode action)
        {
            DictionaryNode command = new DictionaryNode() {
                { "Request", action }
            };

            DictionaryNode response = Service.SendReceivePlist(command)?.AsDictionaryNode() ?? new DictionaryNode();
            if (response.ContainsKey("Status") && response["Status"].AsStringNode().Value != "Success") {
                throw new Exception($"Failed to perform action: {action.Value}");
            }
            return response["Diagnostics"];
        }

        private DictionaryNode IORegistry(string? plane = null, string? name = null, string? ioClass = null)
        {
            DictionaryNode dict = new DictionaryNode() {
                { "Request", new StringNode("IORegistry") }
            };

            if (!string.IsNullOrWhiteSpace(plane)) {
                dict.Add("CurrentPlane", new StringNode(plane));
            }
            if (!string.IsNullOrWhiteSpace(name)) {
                dict.Add("EntryName", new StringNode(name));
            }
            if (!string.IsNullOrWhiteSpace(ioClass)) {
                dict.Add("EntryClass", new StringNode(ioClass));
            }

            DictionaryNode response = Service.SendReceivePlist(dict)?.AsDictionaryNode() ?? new DictionaryNode();
            if (response.ContainsKey("Status") && response["Status"].AsStringNode().Value != "Success") {
                throw new Exception($"Got invalid response: {response}");
            }

            if (response.ContainsKey("Diagnostics")) {
                DictionaryNode diagnosticsDict = response["Diagnostics"].AsDictionaryNode();
                return diagnosticsDict["IORegistry"].AsDictionaryNode();
            }
            return new DictionaryNode();
        }

        public DictionaryNode GetBattery()
        {
            return IORegistry(null, null, "IOPMPowerSource");
        }

        public Dictionary<string, ulong> GetStorageDetails()
        {
            Dictionary<string, ulong> storageData = new();
            DictionaryNode storageList = MobileGestalt(new List<string>() { "DiskUsage" });
            if (storageList.ContainsKey("DiskUsage")) {
                foreach (KeyValuePair<string, PropertyNode> kvp in storageList["DiskUsage"].AsDictionaryNode()) {
                    storageData.Add(kvp.Key, kvp.Value.AsIntegerNode().Value);
                }
            }
            return storageData;
        }

        public PropertyNode Info(string diagnosticType = "All")
        {
            return ExecuteCommand(new StringNode(diagnosticType));
        }

        public DictionaryNode MobileGestalt(List<string> keys)
        {
            DictionaryNode request = new DictionaryNode() {
                { "Request", new StringNode("MobileGestalt") },
            };
            ArrayNode mobileGestaltKeys = new ArrayNode();
            foreach (string key in keys) {
                mobileGestaltKeys.Add(new StringNode(key));
            }
            request.Add("MobileGestaltKeys", mobileGestaltKeys);

            DictionaryNode response = Service.SendReceivePlist(request)?.AsDictionaryNode() ?? new DictionaryNode();
            if (response.ContainsKey("Status") && response["Status"].AsStringNode().Value != "Success") {
                throw new Exception("Failed to query MobileGestalt");
            }
            if (response.ContainsKey("Diagnostics")) {
                PropertyNode status = response["Diagnostics"].AsDictionaryNode()["MobileGestalt"].AsDictionaryNode()["Status"];
                if (status.AsStringNode().Value != "Success" && status.AsStringNode().Value != "MobileGestaltDeprecated") {
                    throw new Exception("Failed to query MobileGestalt");
                }
            }

            return response["Diagnostics"].AsDictionaryNode()["MobileGestalt"].AsDictionaryNode();
        }

        /// <summary>
        /// Query MobileGestalt using all the available keys
        /// </summary>
        /// <returns></returns>
        public DictionaryNode MobileGestalt()
        {
            return MobileGestalt(_mobileGestaltKeys.ToList());
        }

        public void Restart()
        {
            ExecuteCommand(new StringNode("Restart"));
        }

        public void Shutdown()
        {
            ExecuteCommand(new StringNode("Shutdown"));
        }

        public void Sleep()
        {
            ExecuteCommand(new StringNode("Sleep"));
        }
    }
}

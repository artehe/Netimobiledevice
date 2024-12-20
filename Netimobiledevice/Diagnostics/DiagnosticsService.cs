using Microsoft.Extensions.Logging;
using Netimobiledevice.Exceptions;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Plist;
using System;
using System.Collections.Generic;

namespace Netimobiledevice.Diagnostics
{
    /// <summary>
    /// Provides a service to query MobileGestalt & IORegistry keys, as well functionality to
    /// reboot, shutdown, or put the device into sleep mode.
    /// </summary>
    public sealed class DiagnosticsService(LockdownServiceProvider lockdown, ILogger? logger = null) : LockdownService(lockdown, ServiceNameUsed, GetDiagnosticsServiceConnection(lockdown), logger: logger)
    {
        private const string LOCKDOWN_SERVICE_NAME_NEW = "com.apple.mobile.diagnostics_relay";
        private const string LOCKDOWN_SERVICE_NAME_OLD = "com.apple.iosdiagnostics.relay";
        private const string RSD_SERVICE_NAME = "com.apple.mobile.diagnostics_relay.shim.remote";

        private readonly string[] _mobileGestaltKeys = [
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
        ];

        private static string ServiceNameUsed { get; set; } = LOCKDOWN_SERVICE_NAME_NEW;

        private static ServiceConnection? GetDiagnosticsServiceConnection(LockdownServiceProvider lockdown)
        {
            ServiceConnection? service = null;
            if (lockdown is LockdownClient) {
                try {
                    service = lockdown.StartLockdownService(LOCKDOWN_SERVICE_NAME_NEW);
                    ServiceNameUsed = LOCKDOWN_SERVICE_NAME_NEW;
                }
                catch (Exception ex) {
                    lockdown.Logger.LogWarning(ex, "Failed to start the new lockdown service, falling back to the old service.");
                    service = lockdown.StartLockdownService(LOCKDOWN_SERVICE_NAME_OLD);
                    ServiceNameUsed = LOCKDOWN_SERVICE_NAME_OLD;
                }
            }
            else {
                ServiceNameUsed = RSD_SERVICE_NAME;
            }
            return service;
        }

        private PropertyNode ExecuteCommand(StringNode action)
        {
            DictionaryNode command = new DictionaryNode() {
                { "Request", action }
            };

            DictionaryNode response = Service.SendReceivePlist(command)?.AsDictionaryNode() ?? [];
            if (response.ContainsKey("Status") && response["Status"].AsStringNode().Value != "Success") {
                throw new DiagnosticsException($"Failed to perform action: {action.Value}");
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

            DictionaryNode response = Service.SendReceivePlist(dict)?.AsDictionaryNode() ?? [];
            if (response.ContainsKey("Status") && response["Status"].AsStringNode().Value != "Success") {
                throw new DiagnosticsException($"Got invalid response: {response}");
            }

            if (response.ContainsKey("Diagnostics")) {
                DictionaryNode diagnosticsDict = response["Diagnostics"].AsDictionaryNode();
                return diagnosticsDict["IORegistry"].AsDictionaryNode();
            }
            return [];
        }

        public DictionaryNode GetBattery()
        {
            return IORegistry(null, null, "IOPMPowerSource");
        }

        public Dictionary<string, ulong> GetStorageDetails()
        {
            Dictionary<string, ulong> storageData = [];
            DictionaryNode storageList = MobileGestalt(["DiskUsage"]);
            if (storageList.ContainsKey("DiskUsage")) {
                foreach (KeyValuePair<string, PropertyNode> kvp in storageList["DiskUsage"].AsDictionaryNode()) {
                    storageData.Add(kvp.Key, kvp.Value.AsIntegerNode().Value);
                }
            }
            return storageData;
        }

        public Dictionary<string, object> GetBatteryDetails()
        {
            Dictionary<string, object> batteryDetails = [];
            List<string> keys = ["BatteryCurrentCapacity", "BatteryIsCharging", "BatteryIsFullyCharged", "BatterySerialNumber"];
            DictionaryNode batteryData = MobileGestalt(keys);

            foreach (string key in keys) {
                if (batteryData.ContainsKey(key)) {
                    PropertyNode valueNode = batteryData[key];
                    batteryDetails.Add(key, GetValueFromNode(valueNode));
                }
            }

            return batteryDetails;
        }

        private object GetValueFromNode(PropertyNode node)
        {
            return node switch {
                IntegerNode intValueNode => intValueNode.Value,
                BooleanNode boolValueNode => boolValueNode.Value,
                StringNode stringValueNode => stringValueNode.Value,
                _ => null,
            };
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
            ArrayNode mobileGestaltKeys = [];
            foreach (string key in keys) {
                mobileGestaltKeys.Add(new StringNode(key));
            }
            request.Add("MobileGestaltKeys", mobileGestaltKeys);

            DictionaryNode response = Service.SendReceivePlist(request)?.AsDictionaryNode() ?? [];
            if (response.ContainsKey("Status") && response["Status"].AsStringNode().Value != "Success") {
                throw new DiagnosticsException("Failed to query MobileGestalt");
            }
            if (response.ContainsKey("Diagnostics")) {
                PropertyNode status = response["Diagnostics"].AsDictionaryNode()["MobileGestalt"].AsDictionaryNode()["Status"];
                if (status.AsStringNode().Value == "MobileGestaltDeprecated") {
                    throw new DeprecatedException("Failed to query MobileGestalt; deprecated as of iOS >= 17.4.");
                }
                else if (status.AsStringNode().Value != "Success") {
                    throw new DiagnosticsException("Failed to query MobileGestalt");
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
            return MobileGestalt([.. _mobileGestaltKeys]);
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

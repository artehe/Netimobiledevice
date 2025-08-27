using Microsoft.Extensions.Logging;
using Netimobiledevice.Lockdown.Pairing;
using Netimobiledevice.Plist;
using Netimobiledevice.Usbmuxd;
using System.IO;

namespace Netimobiledevice.Lockdown
{
    public class PlistUsbmuxLockdownClient : UsbmuxLockdownClient
    {
        public PlistUsbmuxLockdownClient(ServiceConnection service, string hostId, string identifier = "", string label = DEFAULT_CLIENT_NAME, string systemBuid = SYSTEM_BUID,
            DictionaryNode? pairRecord = null, DirectoryInfo? pairingRecordsCacheDirectory = null, ushort port = SERVICE_PORT, string usbmuxAddress = "", ILogger? logger = null)
            : base(service, hostId, identifier, label, systemBuid, pairRecord, pairingRecordsCacheDirectory, port, usbmuxAddress, logger)
        {
        }

        public override void SavePairRecord()
        {
            base.SavePairRecord();

            long deviceId = _service?.MuxDevice?.DeviceId ?? -1;
            if (_pairRecord is not null) {
                byte[] recordData = PropertyList.SaveAsByteArray(_pairRecord, PlistFormat.Xml);
                using (PlistMuxConnection muxConnection = (PlistMuxConnection) UsbmuxConnection.Create(logger: Logger)) {
                    muxConnection.SavePairRecord(Identifier, deviceId, recordData);
                }
            }
        }

        /// <summary>
        /// Create a LockdownClient instance
        /// </summary>
        /// <param name="service">lockdownd connection handler</param>
        /// <param name="identifier">Used as an identifier to look for the device pair record</param>
        /// <param name="systemBuid">System's unique identifier</param>
        /// <param name="label">lockdownd user-agent</param>
        /// <param name="autopair">Attempt to pair with device (blocking) if not already paired</param>
        /// <param name="pairTimeout">Timeout for autopair</param>
        /// <param name="localHostname">Used as a seed to generate the HostID</param>
        /// <param name="pairRecord">Use this pair record instead of the default behavior (search in host/create our own)</param>
        /// <param name="pairingRecordsCacheFolder">Use the following location to search and save pair records</param>
        /// <param name="port">lockdownd service port</param>
        /// <returns>A new LockdownClient instance</returns>
        public static new PlistUsbmuxLockdownClient Create(ServiceConnection service, string identifier = "", string systemBuid = SYSTEM_BUID, string label = DEFAULT_CLIENT_NAME,
            bool autopair = true, float? pairTimeout = null, string localHostname = "", DictionaryNode? pairRecord = null, string pairingRecordsCacheFolder = "",
            ushort port = SERVICE_PORT, string usbmuxAddress = "", ILogger? logger = null)
        {
            string hostId = PairRecords.GenerateHostId(localHostname);
            DirectoryInfo? pairingRecordsCacheDirectory = PairRecords.GetPairingRecordsCacheFolder(pairingRecordsCacheFolder);

            PlistUsbmuxLockdownClient lockdownClient = new(service, hostId: hostId, identifier: identifier, label: label, systemBuid: systemBuid, pairRecord: pairRecord,
                pairingRecordsCacheDirectory: pairingRecordsCacheDirectory, port: port, usbmuxAddress: usbmuxAddress, logger: logger);

            lockdownClient.HandleAutoPair(autopair, pairTimeout ?? -1);
            return lockdownClient;
        }
    }
}

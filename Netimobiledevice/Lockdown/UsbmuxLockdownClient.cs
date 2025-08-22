using Microsoft.Extensions.Logging;
using Netimobiledevice.Lockdown.Pairing;
using Netimobiledevice.Plist;
using Netimobiledevice.Usbmuxd;
using System.IO;
using System.Threading.Tasks;

namespace Netimobiledevice.Lockdown
{
    public class UsbmuxLockdownClient : LockdownClient
    {
        private readonly string _usbmuxAddress;

        public UsbmuxLockdownClient(ServiceConnection service, string hostId, string identifier = "", string label = DEFAULT_CLIENT_NAME, string systemBuid = SYSTEM_BUID,
            DictionaryNode? pairRecord = null, DirectoryInfo? pairingRecordsCacheDirectory = null, ushort port = SERVICE_PORT, string usbmuxAddress = "", ILogger? logger = null)
            : base(service, hostId, identifier, label, systemBuid, pairRecord, pairingRecordsCacheDirectory, port, logger)
        {
            _usbmuxAddress = usbmuxAddress;
            ConnectionType = UsbmuxdConnectionType.Usb;
        }

        public override async Task<ServiceConnection> CreateServiceConnection(ushort port)
        {
            return await ServiceConnection.CreateUsingUsbmux(Identifier, port, _service?.MuxDevice?.ConnectionType, _usbmuxAddress, Logger).ConfigureAwait(false);
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
        public static UsbmuxLockdownClient Create(ServiceConnection service, string identifier = "", string systemBuid = SYSTEM_BUID, string label = DEFAULT_CLIENT_NAME,
            bool autopair = true, float? pairTimeout = null, string localHostname = "", DictionaryNode? pairRecord = null, string pairingRecordsCacheFolder = "",
            ushort port = SERVICE_PORT, string usbmuxAddress = "", ILogger? logger = null)
        {
            string hostId = PairRecords.GenerateHostId(localHostname);
            DirectoryInfo? pairingRecordsCacheDirectory = PairRecords.GetPairingRecordsCacheFolder(pairingRecordsCacheFolder);

            UsbmuxLockdownClient lockdownClient = new(service, hostId: hostId, identifier: identifier, label: label, systemBuid: systemBuid, pairRecord: pairRecord,
                pairingRecordsCacheDirectory: pairingRecordsCacheDirectory, port: port, usbmuxAddress: usbmuxAddress, logger: logger);

            lockdownClient.HandleAutoPair(autopair, pairTimeout ?? -1);
            return lockdownClient;
        }


        protected override void FetchPairRecord()
        {
            _pairRecord = PairRecords.GetPreferredPairRecord(Identifier, _pairingRecordsCacheDirectory, usbmuxAddress: _usbmuxAddress, logger: Logger);
        }
    }
}

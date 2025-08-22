using Microsoft.Extensions.Logging;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Plist;
using Netimobiledevice.Usbmuxd;
using System.Threading.Tasks;

namespace Netimobiledevice
{
    public static class MobileDevice
    {
        /// <summary>
        /// Create a UsbmuxLockdownClient
        /// </summary>
        /// <param name="serial">Usbmux serial identifier</param>
        /// <param name="identifier">Used as an identifier to look for the device pair record</param>
        /// <param name="label">lockdownd user-agent</param>
        /// <param name="autopair">Attempt to pair with device (blocking) if not already paired</param>
        /// <param name="connectionType">Force a specific type of usbmux connection (USB/Network)</param>
        /// <param name="pairTimeout">Timeout for autopair</param>
        /// <param name="localHostname">Used as a seed to generate the HostID</param>
        /// <param name="pairRecord">Use this pair record instead of the default behavior (search in host/create our own)</param>
        /// <param name="pairingRecordsCacheDir">Use the following location to search and save pair records</param>
        /// <param name="port">lockdownd service port</param>
        /// <param name="usbmuxAddress">usbmuxd address</param>
        /// <returns></returns>
        public static async Task<UsbmuxLockdownClient> CreateUsingUsbmux(string serial = "", string identifier = "", string label = LockdownClient.DEFAULT_CLIENT_NAME,
            bool autopair = true, UsbmuxdConnectionType? connectionType = null, float? pairTimeout = null, string localHostname = "", DictionaryNode? pairRecord = null,
            string pairingRecordsCacheDir = "", ushort port = LockdownClient.SERVICE_PORT, string usbmuxAddress = "", ILogger? logger = null)
        {
            ServiceConnection service = await ServiceConnection.CreateUsingUsbmux(serial, port, connectionType: connectionType, usbmuxAddress: usbmuxAddress, logger).ConfigureAwait(false);

            string systemBuid = string.Empty;

            bool usePlistUsbmuxLockdownClient = false;
            using (UsbmuxConnection client = UsbmuxConnection.Create(usbmuxAddress: usbmuxAddress, logger)) {
                if (client is PlistMuxConnection plistClient) {
                    // Only the Plist version of usbmuxd supports this message type
                    systemBuid = plistClient.GetBuid();
                    usePlistUsbmuxLockdownClient = true;
                }
            }

            if (string.IsNullOrEmpty(identifier)) {
                // Attempt to get identifier from mux device serial
                identifier = service.MuxDevice?.Serial ?? string.Empty;
            }

            if (usePlistUsbmuxLockdownClient) {
                return PlistUsbmuxLockdownClient.Create(service, identifier: identifier, label: label, systemBuid: systemBuid, localHostname: localHostname,
                    pairRecord: pairRecord, pairingRecordsCacheFolder: pairingRecordsCacheDir, pairTimeout: pairTimeout, autopair: autopair, usbmuxAddress: usbmuxAddress,
                    logger: logger);
            }
            return UsbmuxLockdownClient.Create(service, identifier: identifier, label: label, systemBuid: systemBuid, localHostname: localHostname,
                pairRecord: pairRecord, pairingRecordsCacheFolder: pairingRecordsCacheDir, pairTimeout: pairTimeout, autopair: autopair, usbmuxAddress: usbmuxAddress,
                logger: logger);
        }

        /// <summary>
        /// Create a TcpLockdownClient instance over RSD
        /// </summary>
        /// <param name="autopair">Attempt to pair with device (blocking) if not already paired</param>
        /// <param name="identifier">Used as an identifier to look for the device pair record</param>
        /// <param name="label">lockdownd user-agent</param>
        /// <param name="localHostname">Used as a seed to generate the HostID</param>
        /// <param name="pairingRecordsCacheDir">Use the following location to search and save pair records</param>
        /// <param name="pairRecord">Use this pair record instead of the default behavior (search in host/create our own)</param>
        /// <param name="pairTimeout">Timeout for autopair</param>
        /// <param name="port">lockdownd service port</param>
        /// <param name="service">Service connection to use</param>
        /// <returns>RemoteLockdownClient instance</returns>
        public static RemoteLockdownClient CreateUsingRemote(ServiceConnection service, string identifier = "", string label = LockdownClient.DEFAULT_CLIENT_NAME,
            bool autopair = true, float? pairTimeout = null, string localHostname = "", DictionaryNode? pairRecord = null, string pairingRecordsCacheDir = "",
            ushort port = LockdownClient.SERVICE_PORT, ILogger? logger = null)
        {
            RemoteLockdownClient client = RemoteLockdownClient.Create(service, identifier, label: label, localHostname: localHostname, pairRecord: pairRecord,
                pairingRecordsCacheFolder: pairingRecordsCacheDir, pairTimeout: pairTimeout, autopair: autopair, port: port, logger: logger);
            return client;
        }

        /// <summary>
        /// Create a TcpLockdownClient
        /// </summary>
        /// <param name="hostname">The target device hostname</param>
        /// <param name="identifier">Used as an identifier to look for the device pair record</param>
        /// <param name="label">lockdownd user-agent</param>
        /// <param name="autopair">Attempt to pair with device (blocking) if not already paired</param>
        /// <param name="pairTimeout">Timeout for autopair</param>
        /// <param name="localHostname">Used as a seed to generate the HostID</param>
        /// <param name="pairRecord">Use this pair record instead of the default behavior (search in host/create our own)</param>
        /// <param name="pairingRecordsCacheDir">Use the following location to search and save pair records</param>
        /// <param name="port">lockdownd service port</param>
        /// <param name="keepAlive">Use keep-alive to get notified when the connection is lost</param>
        /// <returns></returns>
        public static TcpLockdownClient CreateUsingTcp(string hostname, string identifier = "", string label = LockdownClient.DEFAULT_CLIENT_NAME, bool autopair = true,
            float? pairTimeout = null, string localHostname = "", DictionaryNode? pairRecord = null, string pairingRecordsCacheDir = "",
            ushort port = LockdownClient.SERVICE_PORT, ILogger? logger = null)
        {
            ServiceConnection service = ServiceConnection.CreateUsingTcp(hostname, port, logger);
            TcpLockdownClient client = TcpLockdownClient.Create(service, identifier: identifier, label: label, localHostname: localHostname, pairRecord: pairRecord,
                pairingRecordsCacheFolder: pairingRecordsCacheDir, pairTimeout: pairTimeout, autopair: autopair, port: port, hostname: hostname, logger: logger);
            return client;
        }
    }
}

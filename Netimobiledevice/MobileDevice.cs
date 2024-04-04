using Microsoft.Extensions.Logging;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Plist;
using Netimobiledevice.Usbmuxd;

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
        /// <param name="autoPair">Attempt to pair with device (blocking) if not already paired</param>
        /// <param name="connectionType">Force a specific type of usbmux connection (USB/Network)</param>
        /// <param name="pairTimeout">Timeout for autopair</param>
        /// <param name="localHostname">Used as a seed to generate the HostID</param>
        /// <param name="pairRecord">Use this pair record instead of the default behavior (search in host/create our own)</param>
        /// <param name="pairingRecordsCacheDir">Use the following location to search and save pair records</param>
        /// <param name="port">lockdownd service port</param>
        /// <param name="usbmuxAddress">usbmuxd address</param>
        /// <returns></returns>
        public static UsbmuxLockdownClient CreateUsingUsbmux(string serial = "", string identifier = "", string label = LockdownClient.DEFAULT_CLIENT_NAME,
            bool autopair = true, UsbmuxdConnectionType? connectionType = null, float? pairTimeout = null, string localHostname = "", DictionaryNode? pairRecord = null,
            string pairingRecordsCacheDir = "", ushort port = LockdownClient.SERVICE_PORT, string usbmuxAddress = "", ILogger? logger = null)
        {
            ServiceConnection service = ServiceConnection.CreateUsingUsbmux(serial, port, connectionType: connectionType, usbmuxAddress: usbmuxAddress, logger);

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
    }
}

/* TODO

def create_using_tcp(hostname: str, identifier: str = None, label: str = DEFAULT_LABEL, autopair: bool = True,
                     pair_timeout: float = None, local_hostname: str = None, pair_record: Mapping = None,
                     pairing_records_cache_folder: Path = None, port: int = SERVICE_PORT,
                     keep_alive: bool = False) -> TcpLockdownClient:
    """
    Create a TcpLockdownClient instance

    :param hostname: The target device hostname
    :param identifier: Used as an identifier to look for the device pair record
    :param label: lockdownd user-agent
    :param autopair: Attempt to pair with device (blocking) if not already paired
    :param pair_timeout: Timeout for autopair
    :param local_hostname: Used as a seed to generate the HostID
    :param pair_record: Use this pair record instead of the default behavior (search in host/create our own)
    :param pairing_records_cache_folder: Use the following location to search and save pair records
    :param port: lockdownd service port
    :param keep_alive: use keep-alive to get notified when the connection is lost
    :return: TcpLockdownClient instance
    """
    service = ServiceConnection.create_using_tcp(hostname, port, keep_alive=keep_alive)
    client = TcpLockdownClient.create(
        service, identifier=identifier, label=label, local_hostname=local_hostname, pair_record=pair_record,
        pairing_records_cache_folder=pairing_records_cache_folder, pair_timeout=pair_timeout, autopair=autopair,
        port=port, hostname=hostname, keep_alive=keep_alive)
    return client


def create_using_remote(service: ServiceConnection, identifier: str = None, label: str = DEFAULT_LABEL,
                        autopair: bool = True, pair_timeout: float = None, local_hostname: str = None,
                        pair_record: Mapping = None, pairing_records_cache_folder: Path = None,
                        port: int = SERVICE_PORT) -> RemoteLockdownClient:
    """
    Create a TcpLockdownClient instance over RSD

    :param hostname: The target device hostname
    :param identifier: Used as an identifier to look for the device pair record
    :param label: lockdownd user-agent
    :param autopair: Attempt to pair with device (blocking) if not already paired
    :param pair_timeout: Timeout for autopair
    :param local_hostname: Used as a seed to generate the HostID
    :param pair_record: Use this pair record instead of the default behavior (search in host/create our own)
    :param pairing_records_cache_folder: Use the following location to search and save pair records
    :param port: lockdownd service port
    :return: TcpLockdownClient instance
    """
    client = RemoteLockdownClient.create(
        service, identifier=identifier, label=label, local_hostname=local_hostname, pair_record=pair_record,
        pairing_records_cache_folder=pairing_records_cache_folder, pair_timeout=pair_timeout, autopair=autopair,
        port=port)
    return client
*/

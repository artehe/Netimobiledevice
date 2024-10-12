using Netimobiledevice.Remoted.Bonjour;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Zeroconf;

namespace Netimobiledevice.Remoted
{
    public static class Remote
    {
        private static async Task<List<dynamic>> BrowseRsd(int timeout)
        {
            // TODO remove the dynamic from this and replace with a fixed data class / record
            List<dynamic> devices = new List<dynamic>();
            foreach (RemoteServiceDiscoveryService rsd in await GetRsds(timeout)) {
                /* TODO
                    devices.append({'address': rsd.service.address[0],
                                    'port': RSD_PORT,
                                    'UniqueDeviceID': rsd.peer_info['Properties']['UniqueDeviceID'],
                                    'ProductType': rsd.peer_info['Properties']['ProductType'],
                                    'OSVersion': rsd.peer_info['Properties']['OSVersion']})
                */
            }
            return devices;
        }

        private static async Task<List<RemoteServiceDiscoveryService>> GetRsds(int timeout, string? udid = null)
        {
            List<RemoteServiceDiscoveryService> result = new List<RemoteServiceDiscoveryService>();
            try {
                // TODO stop_remoted_if_required();
                foreach (IZeroconfHost answer in await BonjourService.BrowseRemoted(timeout)) {
                    foreach (string ip in answer.IPAddresses) {
                        RemoteServiceDiscoveryService rsd = new RemoteServiceDiscoveryService(ip, RemoteServiceDiscoveryService.RSD_PORT);
                        try {
                            await rsd.Connect().ConfigureAwait(false);
                        }
                        catch (Exception ex) {
                            Debug.WriteLine("Error trying to parse browse data");
                            Debug.WriteLine(ex);
                            continue;
                        }

                        if (string.IsNullOrEmpty(udid) || rsd.Udid == udid) {
                            result.Add(rsd)
                        }
                        else {
                            await rsd.Close();
                        }
                    }
                }
            }
            finally {
                // TODO resume_remoted_if_required();
            }
            return result;
        }

        /// <summary>
        /// Browse RemoteXPC devices using bonjour 
        /// </summary>
        /// <param name="timeout">Bonjour timeout (in seconds)</param>
        /// <returns></returns>
        public static async Task Browse(int timeout = BonjourService.DEFAULT_BONJOUR_TIMEOUT)
        {
            var usbDevices = await BrowseRsd(timeout).ConfigureAwait(false);
            // TODO var wifiDevices = await BrowseRemotePairing(timeout);
            // TODO return a dictionary response of these objects so we can write out the result in the demo
            /* TODO
        print_json({
            'usb': await browse_rsd(timeout),
            'wifi': await browse_remotepairing(timeout),
        })
            result = 
            {
                'usb': [],
                'wifi': []
            }
            */
        }
    }
}
using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Zeroconf;

namespace Netimobiledevice.Remoted.Bonjour
{
    public static class BonjourService
    {
#if WINDOWS
        public const int DEFAULT_BONJOUR_TIMEOUT = 2;
#else
        public const int DEFAULT_BONJOUR_TIMEOUT = 1;
#endif

        private static string[] RemotedServiceNames => ["_remoted._tcp.local."];

        private static List<NetworkInterface> GetIPv6Interfaces()
        {
            List<NetworkInterface> ipv6Interfaces = new List<NetworkInterface>();
            foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces()) {
                // Check if the adapter is up
                if (adapter.OperationalStatus == OperationalStatus.Up) {
                    // Check if on Windows or if the adapter is a tunnel
                    if (OperatingSystem.IsWindows() || adapter.Description.StartsWith("tun", System.StringComparison.InvariantCulture)) {
                        continue;
                    }

                    foreach (UnicastIPAddressInformation ip in adapter.GetIPProperties().UnicastAddresses) {
                        // Check for IPv6
                        if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6) {
                            ipv6Interfaces.Add(adapter);
                            break;
                        }
                    }
                }
            }
            return ipv6Interfaces;
        }

        public static async Task<List<IZeroconfHost>> Browse(string[] serviceNames, List<NetworkInterface> interfaces, int timeout = DEFAULT_BONJOUR_TIMEOUT)
        {
            List<BonjourListener> listeners = new List<BonjourListener>();
            foreach (NetworkInterface adapter in interfaces) {
                listeners.Add(new BonjourListener(serviceNames, adapter));
            }

            List<IZeroconfHost> answers = new List<IZeroconfHost>();
            await Task.Delay(timeout * 1000).ConfigureAwait(false);

            foreach (BonjourListener listener in listeners) {
                IZeroconfHost[] answer = await listener.GenerateAnswer().ConfigureAwait(false);
                answers.AddRange(answer);
            }

            return answers;
        }

        public static async Task<List<IZeroconfHost>> BrowseIpv6(string[] serviceNames, int timeout = DEFAULT_BONJOUR_TIMEOUT)
        {
            return await Browse(serviceNames, GetIPv6Interfaces(), timeout);
        }

        public static async Task<List<IZeroconfHost>> BrowseRemoted(int timeout = DEFAULT_BONJOUR_TIMEOUT)
        {
            return await BrowseIpv6(RemotedServiceNames, timeout);
        }
    }
}

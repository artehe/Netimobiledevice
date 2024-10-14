using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;

namespace Netimobiledevice.Remoted
{
    internal static class Utils
    {
        public static List<NetworkInterface> GetIPv6Interfaces()
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
    }
}

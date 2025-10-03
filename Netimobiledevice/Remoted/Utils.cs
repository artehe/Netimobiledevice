using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;

namespace Netimobiledevice.Remoted;

internal static class Utils {
    public static List<NetworkInterface> GetIPv4Interfaces() {
        List<NetworkInterface> ipv4Interfaces = [];
        foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces()) {
            // Check if the adapter is up
            if (adapter.OperationalStatus == OperationalStatus.Up) {
                // Check if the adapter is a tunnel
                if (adapter.Description.StartsWith("tun", StringComparison.InvariantCulture)) {
                    continue;
                }

                foreach (UnicastIPAddressInformation ip in adapter.GetIPProperties().UnicastAddresses) {
                    // Check for IPv4
                    if (ip.Address.ToString() == "127.0.0.1") {
                        continue;
                    }

                    if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) {
                        ipv4Interfaces.Add(adapter);
                    }
                }
            }
        }
        return ipv4Interfaces;
    }

    public static List<NetworkInterface> GetIPv6Interfaces() {
        List<NetworkInterface> ipv6Interfaces = [];
        foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces()) {
            // Check if the adapter is up
            if (adapter.OperationalStatus == OperationalStatus.Up) {
                // Check if the adapter is a tunnel
                if (adapter.Description.StartsWith("tun", StringComparison.InvariantCulture)) {
                    continue;
                }

                foreach (UnicastIPAddressInformation ip in adapter.GetIPProperties().UnicastAddresses) {
                    // Check for IPv6
                    if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6) {
                        ipv6Interfaces.Add(adapter);
                    }
                }
            }
        }
        return ipv6Interfaces;
    }
}

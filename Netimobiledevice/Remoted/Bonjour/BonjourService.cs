using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using Zeroconf;

namespace Netimobiledevice.Remoted.Bonjour;

public static class BonjourService {
#if WINDOWS
    public const int DEFAULT_BONJOUR_TIMEOUT = 2000;
#else
    public const int DEFAULT_BONJOUR_TIMEOUT = 1000;
#endif

    public static string[] Mobdev2ServiceNames => ["_apple-mobdev2._tcp.local."];
    public static string[] RemotedServiceNames => ["_remoted._tcp.local."];

    private static List<NetworkInterface> GetIpv4Addresses() {
        List<NetworkInterface> ips = [];
        foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces()) {
            if (adapter.Name.StartsWith("tun", StringComparison.InvariantCulture)) {
                // Skip browsing on already established tunnels
                continue;
            }
            foreach (UnicastIPAddressInformation ip in adapter.GetIPProperties().UnicastAddresses) {
                if (IPAddress.IsLoopback(ip.Address)) {
                    continue;
                }
                if (ip.Address.AddressFamily != AddressFamily.InterNetwork) {
                    continue;
                }
                ips.Add(adapter);
            }
        }
        return ips;
    }

    private static List<NetworkInterface> GetIpv6Addresses() {
        List<NetworkInterface> ips = [];
        foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces()) {
            if (adapter.Name.StartsWith("tun", StringComparison.InvariantCulture)) {
                // Skip browsing on already established tunnels
                continue;
            }
            foreach (UnicastIPAddressInformation ip in adapter.GetIPProperties().UnicastAddresses) {
                if (IPAddress.IsLoopback(ip.Address)) {
                    continue;
                }
                if (ip.Address.AddressFamily != AddressFamily.InterNetworkV6) {
                    continue;
                }
                ips.Add(adapter);
            }
        }
        return ips;
    }

    public static async Task<List<IZeroconfHost>> Browse(string[] serviceNames, List<NetworkInterface> interfaces, int timeout = DEFAULT_BONJOUR_TIMEOUT) {
        List<BonjourListener> listeners = [];
        foreach (NetworkInterface adapter in interfaces) {
            listeners.Add(new BonjourListener(serviceNames, adapter));
        }

        List<IZeroconfHost> answers = [];
        await Task.Delay(timeout).ConfigureAwait(false);

        foreach (BonjourListener listener in listeners) {
            IZeroconfHost[] answer = await listener.GenerateAnswer().ConfigureAwait(false);
            answers.AddRange(answer);
        }

        return answers;
    }

    public static async Task<List<IZeroconfHost>> BrowseIpv4(string[] serviceNames, int timeout = DEFAULT_BONJOUR_TIMEOUT) {
        return await Browse(serviceNames, Utils.GetIPv4Interfaces(), timeout);
    }

    public static async Task<List<IZeroconfHost>> BrowseIpv6(string[] serviceNames, int timeout = DEFAULT_BONJOUR_TIMEOUT) {
        return await Browse(serviceNames, Utils.GetIPv6Interfaces(), timeout);
    }

    public static async Task<List<IZeroconfHost>> BrouseMobdev2(int timeout = DEFAULT_BONJOUR_TIMEOUT, List<NetworkInterface>? ips = null) {
        ips ??= [.. GetIpv4Addresses(), .. GetIpv6Addresses()];
        return await Browse(Mobdev2ServiceNames, ips, timeout).ConfigureAwait(false);
    }

    public static async Task<List<IZeroconfHost>> BrowseRemoted(int timeout = DEFAULT_BONJOUR_TIMEOUT) {
        return await BrowseIpv6(RemotedServiceNames, timeout);
    }

    public static async Task<List<IZeroconfHost>> BrowseRemotePairing(int timeout = DEFAULT_BONJOUR_TIMEOUT) {
        return await BrowseIpv4(RemotedServiceNames, timeout);
    }
}

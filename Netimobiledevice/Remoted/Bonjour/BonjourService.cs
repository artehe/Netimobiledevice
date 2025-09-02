using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Zeroconf;

namespace Netimobiledevice.Remoted.Bonjour;

public static class BonjourService
{
#if WINDOWS
    public const int DEFAULT_BONJOUR_TIMEOUT = 2000;
#else
    public const int DEFAULT_BONJOUR_TIMEOUT = 1000;
#endif

    public static string[] RemotedServiceNames => ["_remoted._tcp.local."];

    public static async Task<List<IZeroconfHost>> Browse(string[] serviceNames, List<NetworkInterface> interfaces, int timeout = DEFAULT_BONJOUR_TIMEOUT)
    {
        List<BonjourListener> listeners = new List<BonjourListener>();
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

    public static async Task<List<IZeroconfHost>> BrowseIpv4(string[] serviceNames, int timeout = DEFAULT_BONJOUR_TIMEOUT)
    {
        return await Browse(serviceNames, Utils.GetIPv4Interfaces(), timeout);
    }

    public static async Task<List<IZeroconfHost>> BrowseIpv6(string[] serviceNames, int timeout = DEFAULT_BONJOUR_TIMEOUT)
    {
        return await Browse(serviceNames, Utils.GetIPv6Interfaces(), timeout);
    }

    public static async Task<List<IZeroconfHost>> BrowseRemoted(int timeout = DEFAULT_BONJOUR_TIMEOUT)
    {
        return await BrowseIpv6(RemotedServiceNames, timeout);
    }

    public static async Task<List<IZeroconfHost>> BrowseRemotePairing(int timeout = DEFAULT_BONJOUR_TIMEOUT)
    {
        return await BrowseIpv4(RemotedServiceNames, timeout);
    }
}

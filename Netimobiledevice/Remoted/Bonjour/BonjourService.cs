using System.Collections.Generic;
using System.Threading.Tasks;

namespace Netimobiledevice.Remoted.Bonjour;

/// <summary>
/// mDNS browser returning data classes with per-address interface names.
/// Works for any DNS-SD type, e.g. "_remoted._tcp.local."
/// </summary>
public static class BonjourService {
    private const string MOBDEV2_SERVICE_NAME = "_apple-mobdev2._tcp.local.";
    private const string REMOTED_SERVICE_NAME = "_remoted._tcp.local.";
    private const string REMOTEPAIRING_SERVICE_NAME = "_remotepairing._tcp.local.";
    private const string REMOTEPAIRING_MANUAL_PAIRING_SERVICE_NAME = "_remotepairing-manual-pairing._tcp.local.";

#if WINDOWS
    public const int DEFAULT_BONJOUR_TIMEOUT = 2000;
#else
    public const int DEFAULT_BONJOUR_TIMEOUT = 1000;
#endif

    public static async Task<List<ServiceInstance>> BrowseMobdev2Async(int timeout = DEFAULT_BONJOUR_TIMEOUT) {
        MdnsBrowser mdnsBrowser = new();
        return await mdnsBrowser.BrowseService(MOBDEV2_SERVICE_NAME, timeout).ConfigureAwait(false);
    }

    public static async Task<List<ServiceInstance>> BrowseRemotedAsync(int timeout = DEFAULT_BONJOUR_TIMEOUT) {
        MdnsBrowser mdnsBrowser = new();
        return await mdnsBrowser.BrowseService(REMOTED_SERVICE_NAME, timeout).ConfigureAwait(false);
    }

    public static async Task<List<ServiceInstance>> BrowseRemotePairingAsync(int timeout = DEFAULT_BONJOUR_TIMEOUT) {
        MdnsBrowser mdnsBrowser = new();
        return await mdnsBrowser.BrowseService(REMOTEPAIRING_SERVICE_NAME, timeout).ConfigureAwait(false);
    }

    public static async Task<List<ServiceInstance>> BrowseRemotePairingManual(int timeout = DEFAULT_BONJOUR_TIMEOUT) {
        MdnsBrowser mdnsBrowser = new();
        return await mdnsBrowser.BrowseService(REMOTEPAIRING_MANUAL_PAIRING_SERVICE_NAME, timeout).ConfigureAwait(false);
    }
}

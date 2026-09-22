using System.Net.Sockets;

namespace Netimobiledevice.Bonjour;

internal sealed class MdnsInterfaceResolver : IMdnsInterfaceResolver {
    private readonly MdnsNetworkAdapters _adapters = new();

    public string? PickInterface(string ip, AddressFamily family, long? scopeId) {
        return _adapters.PickInterface(ip, family, scopeId);
    }
}

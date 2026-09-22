using Netimobiledevice.Bonjour;
using System.Net.Sockets;

namespace NetimobiledeviceTest.Bonjour;

internal sealed class FakeMdnsInterfaceResolver : IMdnsInterfaceResolver {
    public string? InterfaceName { get; set; }

    public string? PickInterface(string ip, AddressFamily family, long? scopeId) {
        return InterfaceName;
    }
}

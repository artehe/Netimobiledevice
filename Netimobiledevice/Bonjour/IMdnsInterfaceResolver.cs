using System.Net.Sockets;

namespace Netimobiledevice.Bonjour;

public interface IMdnsInterfaceResolver {
    string? PickInterface(string ip, AddressFamily family, long? scopeId);
}

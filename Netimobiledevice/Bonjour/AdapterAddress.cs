using System.Net;

namespace Netimobiledevice.Bonjour;

internal sealed record AdapterAddress(
    string Name,
    string Description,
    IPAddress Address,
    int PrefixLength
);

using System.Net;

namespace Netimobiledevice.Bonjour;

internal sealed record MdnsBrowserRecievedPacket(
    byte[] Data,
    EndPoint Remote
);

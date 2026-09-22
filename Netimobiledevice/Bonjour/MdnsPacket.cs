using System.Net;

namespace Netimobiledevice.Bonjour;

public sealed record MdnsPacket(
    byte[] Data,
    EndPoint RemoteEndPoint);

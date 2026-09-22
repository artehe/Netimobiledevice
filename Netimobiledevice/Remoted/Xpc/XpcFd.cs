using System;

namespace Netimobiledevice.Remoted.Xpc;

public class XpcFd(uint data) : XpcObject<uint>(data)
{
    public override bool IsAligned => false;

    public override bool IsPrefixed => false;

    public override XpcMessageType Type => XpcMessageType.Fd;

    public static XpcFd Deserialise(byte[] data)
    {
        uint value = BitConverter.ToUInt32(data, 0);
        return new XpcFd(value);
    }

    public override byte[] Serialise()
    {
        return BitConverter.GetBytes(Data);
    }
}

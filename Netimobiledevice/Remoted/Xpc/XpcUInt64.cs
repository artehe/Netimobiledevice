using System;

namespace Netimobiledevice.Remoted.Xpc;

public class XpcUInt64(ulong data) : XpcObject<ulong>(data)
{
    public override bool IsAligned => false;
    public override bool IsPrefixed => false;

    public override XpcMessageType Type => XpcMessageType.Uint64;

    public static XpcUInt64 Deserialise(byte[] data)
    {
        ulong value = BitConverter.ToUInt64(data, 0);
        return new XpcUInt64(value);
    }

    public override byte[] Serialise()
    {
        return BitConverter.GetBytes(Data);
    }
}

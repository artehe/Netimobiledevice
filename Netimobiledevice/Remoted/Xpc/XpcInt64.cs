using System;

namespace Netimobiledevice.Remoted.Xpc;

public class XpcInt64(long data) : XpcObject<long>(data)
{
    public override bool IsAligned => false;
    public override bool IsPrefixed => false;

    public override XpcMessageType Type => XpcMessageType.Int64;

    public static XpcInt64 Deserialise(byte[] data)
    {
        long value = BitConverter.ToInt64(data, 0);
        return new XpcInt64(value);
    }

    public override byte[] Serialise()
    {
        return BitConverter.GetBytes(Data);
    }
}

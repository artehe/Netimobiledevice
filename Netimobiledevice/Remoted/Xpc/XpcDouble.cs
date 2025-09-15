using System;

namespace Netimobiledevice.Remoted.Xpc;

public class XpcDouble(double data) : XpcObject<double>(data)
{
    public override bool IsAligned => false;

    public override bool IsPrefixed => false;

    public override XpcMessageType Type => XpcMessageType.Double;

    public static XpcDouble Deserialise(byte[] data)
    {
        double value = BitConverter.ToDouble(data, 0);
        return new XpcDouble(value);
    }

    public override byte[] Serialise()
    {
        return BitConverter.GetBytes(Data);
    }
}

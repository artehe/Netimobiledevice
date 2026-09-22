namespace Netimobiledevice.Remoted.Xpc;

public class XpcData(byte[]? data) : XpcObject<byte[]>(data)
{
    public override bool IsAligned => true;

    public override bool IsPrefixed => true;

    public override XpcMessageType Type => XpcMessageType.Data;

    public static XpcData Deserialise(byte[] data)
    {
        data = GetPrefixSizeFromData(data);
        return new XpcData(data);
    }

    public override byte[] Serialise()
    {
        return Data ?? [];
    }
}

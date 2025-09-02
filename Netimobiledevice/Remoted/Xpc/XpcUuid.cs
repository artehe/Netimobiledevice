using System;
using System.Linq;

namespace Netimobiledevice.Remoted.Xpc;

public class XpcUuid : XpcObject
{
    public override bool IsAligned => false;

    public override bool IsPrefixed => false;

    public override XpcMessageType Type => XpcMessageType.Uuid;

    public Guid Data { get; }

    public XpcUuid(byte[] data)
    {
        Data = new Guid(data.Take(16).ToArray());
    }

    public static XpcUuid Deserialise(byte[] data)
    {
        return new XpcUuid(data);
    }

    public override byte[] Serialise()
    {
        return Data.ToByteArray();
    }
}

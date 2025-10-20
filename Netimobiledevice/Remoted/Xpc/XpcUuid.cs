using System;
using System.Linq;

namespace Netimobiledevice.Remoted.Xpc;

public class XpcUuid(byte[] data) : XpcObject {
    public override bool IsAligned => false;

    public override bool IsPrefixed => false;

    public override XpcMessageType Type => XpcMessageType.Uuid;

    public Guid Data { get; } = new Guid([.. data.Take(16)]);

    public static XpcUuid Deserialise(byte[] data) {
        return new XpcUuid(data);
    }

    public override byte[] Serialise() {
        return Data.ToByteArray();
    }
}

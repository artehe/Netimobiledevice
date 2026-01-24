namespace Netimobiledevice.Remoted.Xpc;

public class XpcNull : XpcObject {
    public override bool IsAligned => false;

    public override bool IsPrefixed => false;

    public override XpcMessageType Type => XpcMessageType.Null;

    public static XpcNull Deserialise(byte[] _) {
        return new XpcNull();
    }

    public override byte[] Serialise() {
        return [];
    }
}

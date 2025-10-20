using System;

namespace Netimobiledevice.Remoted.Xpc;

public class XpcDate(DateTime data) : XpcObject<DateTime>(data) {

    public override bool IsAligned => throw new NotImplementedException();

    public override bool IsPrefixed => throw new NotImplementedException();

    public override XpcMessageType Type => throw new NotImplementedException();

    public override byte[] Serialise() {
        throw new NotImplementedException();
    }
}

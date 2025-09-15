using System;

namespace Netimobiledevice.Remoted.Xpc;

public class XpcDate : XpcObject<DateTime>
{

    public override bool IsAligned => throw new NotImplementedException();

    public override bool IsPrefixed => throw new NotImplementedException();

    public override XpcMessageType Type => throw new NotImplementedException();
    public XpcDate(DateTime data) : base(data)
    {
    }

    public override byte[] Serialise()
    {
        throw new NotImplementedException();
    }
}

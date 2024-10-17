namespace Netimobiledevice.Remoted.Xpc
{
    public class XpcInt64 : XpcObject<long>
    {
        public override bool IsAligned => false;
        public override bool IsPrefixed => false;

        public override XpcMessageType Type => XpcMessageType.Int64;

        public XpcInt64() { }

        public XpcInt64(long value)
        {
            Data = value;
        }
    }
}

namespace Netimobiledevice.Remoted.Xpc
{
    public class XpcNullObject : XpcObject<object>
    {
        public override XpcMessageType Type => XpcMessageType.Null;

        public override object? Data => null;
    }
}
namespace Netimobiledevice.Remoted.Xpc
{
    public class XpcData(byte[]? data) : XpcObject<byte[]>(data)
    {
        public override bool IsAligned => throw new System.NotImplementedException();

        public override bool IsPrefixed => throw new System.NotImplementedException();

        public override XpcMessageType Type => throw new System.NotImplementedException();

        public override byte[] Serialise()
        {
            throw new System.NotImplementedException();
        }
    }
}

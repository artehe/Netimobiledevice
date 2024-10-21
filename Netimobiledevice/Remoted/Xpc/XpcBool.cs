using System;

namespace Netimobiledevice.Remoted.Xpc
{
    public class XpcBool(bool data) : XpcObject<bool>(data)
    {
        public override bool IsAligned => false;
        public override bool IsPrefixed => false;

        public override XpcMessageType Type => XpcMessageType.Bool;

        public static XpcBool Deserialise(byte[] data)
        {
            int entry = BitConverter.ToInt32(data);
            if (entry > 0) {
                return new XpcBool(true);
            }
            return new XpcBool(false);
        }

        public override byte[] Serialise()
        {
            if (Data) {
                return [0, 0, 0, 1];
            }
            return [0, 0, 0, 0];
        }
    }
}

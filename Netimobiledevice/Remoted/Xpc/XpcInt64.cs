using System;

namespace Netimobiledevice.Remoted.Xpc
{
    internal class XpcInt64 : XpcObject<long>
    {
        public override XpcMessageType Type => XpcMessageType.Int64;

        public override byte[] Serialise()
        {
            return BitConverter.GetBytes(Data);
        }
    }
}

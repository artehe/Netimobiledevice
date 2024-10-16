using System;

namespace Netimobiledevice.Remoted.Xpc
{
    public class XpcPayload
    {
        public uint Magic => 0x42133742;
        public uint ProtocolVersion => 0x00000005;
        public XpcObject Obj { get; set; }

        public byte[] Serialise()
        {
            return [
                .. BitConverter.GetBytes(Magic),
                .. BitConverter.GetBytes(ProtocolVersion),
                .. Obj.Serialise(),
            ];
        }
    }
}

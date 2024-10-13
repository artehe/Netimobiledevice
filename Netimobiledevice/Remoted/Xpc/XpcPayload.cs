using System;
using System.Collections.Generic;

namespace Netimobiledevice.Remoted.Xpc
{
    public class XpcPayload
    {
        public uint Magic => 0x42133742;
        public uint ProtocolVersion => 0x00000005;
        public XpcObject Obj { get; set; }

        public byte[] Serialise()
        {
            List<byte> payload = new List<byte>();
            payload.AddRange(BitConverter.GetBytes(Magic));
            payload.AddRange(BitConverter.GetBytes(ProtocolVersion));
            payload.AddRange(Obj.Serialise());
            return payload.ToArray();
        }
    }
}
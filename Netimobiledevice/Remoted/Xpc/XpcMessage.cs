using System;
using System.Collections.Generic;

namespace Netimobiledevice.Remoted.Xpc
{
    public class XpcMessage
    {
        public uint MessageId { get; set; }
        public XpcPayload Payload { get; set; }

        public byte[] Serialise()
        {
            List<byte> payload = new List<byte>();
            payload.AddRange(BitConverter.GetBytes(MessageId));
            payload.AddRange(Payload.Serialise());
            return payload.ToArray();
        }
    }
}
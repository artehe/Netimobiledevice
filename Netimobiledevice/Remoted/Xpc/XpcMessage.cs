using System;

namespace Netimobiledevice.Remoted.Xpc
{
    public class XpcMessage
    {
        public ulong MessageId { get; set; }
        public XpcPayload Payload { get; set; }

        public byte[] Serialise()
        {
            byte[] payload = Payload.Serialise();
            byte[] messageSize = BitConverter.GetBytes(payload.LongLength);
            return [
                .. messageSize,
                .. BitConverter.GetBytes(MessageId),
                .. payload
            ];
        }
    }
}

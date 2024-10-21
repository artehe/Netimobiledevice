using System;
using System.Linq;

namespace Netimobiledevice.Remoted.Xpc
{
    public class XpcMessage
    {
        public ulong MessageId { get; set; }
        public XpcPayload? Payload { get; set; }

        public static XpcMessage Deserialise(byte[] data)
        {
            ulong messageSize = BitConverter.ToUInt64(data, 0);
            ulong messageId = BitConverter.ToUInt64(data, 8);
            return new XpcMessage() {
                MessageId = messageId,
                Payload = XpcPayload.Deserialise(data.Skip(16).ToArray())
            };
        }

        public byte[] Serialise()
        {
            byte[] payload = Payload?.Serialise() ?? [];
            byte[] messageSize = BitConverter.GetBytes(payload.LongLength);
            return [
                .. messageSize,
                .. BitConverter.GetBytes(MessageId),
                .. payload
            ];
        }
    }
}

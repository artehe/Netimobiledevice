using System;
using System.Linq;

namespace Netimobiledevice.Remoted.Xpc
{
    public class XpcPayload
    {
        public uint Magic => 0x42133742;
        public uint ProtocolVersion => 0x00000005;
        public XpcObject Obj { get; set; }

        public static XpcPayload Deserialise(byte[] data)
        {
            uint magic = BitConverter.ToUInt32(data, 0);
            uint protocolVersion = BitConverter.ToUInt32(data, 4);

            XpcPayload payload = new XpcPayload();
            if (magic != payload.Magic) {
                throw new DataMisalignedException($"Missing correct magic got {magic} instead of {payload.Magic}");
            }
            if (protocolVersion != payload.ProtocolVersion) {
                throw new InvalidOperationException($"Unexpected protocol version go {protocolVersion} rather than {payload.ProtocolVersion}");
            }

            payload.Obj = XpcSerialiser.Deserialise(data.Skip(8).ToArray());
            return payload;
        }

        public byte[] Serialise()
        {
            return [
                .. BitConverter.GetBytes(Magic),
                .. BitConverter.GetBytes(ProtocolVersion),
                .. XpcSerialiser.Serialise(Obj),
            ];
        }
    }
}

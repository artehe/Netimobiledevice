using System;
using System.Text;

namespace Netimobiledevice.Remoted.Tunnel
{
    internal class CDTunnelPacket
    {
        private byte[] EncodedBody => Encoding.UTF8.GetBytes(JsonBody);

        public static ulong Magic => 0x434454756e6e656c;
        public ushort Length => (ushort) EncodedBody.Length;
        public string JsonBody { get; }

        public CDTunnelPacket(string jsonString)
        {
            JsonBody = jsonString;
        }

        public byte[] GetBytes()
        {
            return [.. BitConverter.GetBytes(Magic), .. BitConverter.GetBytes(Length), .. EncodedBody];
        }

        public static CDTunnelPacket Parse(byte[] data)
        {
            byte[] magicBytes = BitConverter.GetBytes(Magic);
            for (int i = 0; i < magicBytes.Length; i++) {
                if (magicBytes[i] != data[i]) {
                    throw new Exception("Data mismatch");
                }
            }
            ushort length = BitConverter.ToUInt16(data, magicBytes.Length);
            string json = Encoding.UTF8.GetString(data, magicBytes.Length + sizeof(ushort), length);
            return new CDTunnelPacket(json);
        }
    }
}

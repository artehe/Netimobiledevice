using Netimobiledevice.Extentions;
using System;
using System.Buffers.Binary;
using System.Linq;
using System.Text;

namespace Netimobiledevice.Remoted.Tunnel;

internal class CDTunnelPacket {
    private byte[] EncodedBody => Encoding.UTF8.GetBytes(JsonBody);

    public static byte[] Magic => Encoding.UTF8.GetBytes("CDTunnel");
    public ushort Length => (ushort) EncodedBody.Length;
    public string JsonBody { get; }

    public CDTunnelPacket(string jsonString) {
        JsonBody = jsonString;
    }

    public byte[] GetBytes() {
        ushort bodyLength = (ushort) EncodedBody.Length;
        return [.. Magic, .. BitConverter.GetBytes(bodyLength).EnsureBigEndian(), .. EncodedBody];
    }

    public static CDTunnelPacket Parse(byte[] data) {
        for (int i = 0; i < Magic.Length; i++) {
            if (Magic[i] != data[i]) {
                throw new NetimobiledeviceException("Data mismatch");
            }
        }
        ushort length = BinaryPrimitives.ReadUInt16BigEndian(data.Skip(Magic.Length).ToArray());
        string json = Encoding.UTF8.GetString(data, Magic.Length + sizeof(ushort), length);
        return new CDTunnelPacket(json);
    }
}

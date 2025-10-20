using Netimobiledevice.EndianBitConversion;
using Netimobiledevice.Extentions;
using System;
using System.Text;

namespace Netimobiledevice.Remoted.Tunnel;

internal class CoreDeviceTunnelPacket {
    private byte[] EncodedBody => Encoding.UTF8.GetBytes(JsonBody);

    public static byte[] Magic => Encoding.UTF8.GetBytes("CDTunnel");
    public ushort Length => (ushort) EncodedBody.Length;
    public string JsonBody { get; }

    public CoreDeviceTunnelPacket(string jsonString) {
        JsonBody = jsonString;
    }

    public byte[] GetBytes() {
        ushort bodyLength = (ushort) EncodedBody.Length;
        return [.. Magic, .. BitConverter.GetBytes(bodyLength).EnsureBigEndian(), .. EncodedBody];
    }

    public static CoreDeviceTunnelPacket Parse(byte[] data) {
        for (int i = 0; i < Magic.Length; i++) {
            if (Magic[i] != data[i]) {
                throw new Exception("Data mismatch");
            }
        }
        ushort length = EndianBitConverter.BigEndian.ToUInt16(data, Magic.Length);
        string json = Encoding.UTF8.GetString(data, Magic.Length + sizeof(ushort), length);
        return new CoreDeviceTunnelPacket(json);
    }
}

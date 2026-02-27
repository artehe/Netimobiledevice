using Netimobiledevice.EndianBitConversion;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Netimobiledevice.Remoted.Bonjour;

internal static class DnsHelpers {
    private const ushort CLASS_IN = 0x0001;
    /// <summary>
    /// Unicast-response bit (we use multicast queries)
    /// </summary>
    private const ushort CLASS_QU = 0x8000;

    public const ushort QTYPE_A = 1;
    public const ushort QTYPE_AAAA = 28;
    public const ushort QTYPE_PTR = 12;
    public const ushort QTYPE_SRV = 33;
    public const ushort QTYPE_TXT = 16;

    private static byte[] EncodeName(string name) {
        List<byte> result = [];

        name = name.TrimEnd('.');
        foreach (string label in name.Split('.')) {
            byte[] bytes = Encoding.UTF8.GetBytes(label);
            if (bytes.Length > 63) {
                throw new ArgumentException("Label too long");
            }
            result.AddRange(EndianBitConverter.LittleEndian.GetBytes(bytes.Length));
            result.AddRange(bytes);
        }

        result.Add(0);
        return [.. result];
    }

    public static byte[] BuildQuery(string name, ushort qtype, bool unicast = false) {
        // TXID=0, flags=0, QD=1
        byte[] qd = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(1));
        byte[] header = new byte[12];
        header[4] = qd[0];
        header[5] = qd[1];

        ushort qclass = (ushort) (CLASS_IN | (unicast ? CLASS_QU : 0));

        var query = new List<byte>(header);
        query.AddRange(EncodeName(name));
        query.AddRange(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(qtype)));
        query.AddRange(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(qclass)));
        return [.. query];
    }

    public static (string name, int offset) DecodeName(byte[] data, int offset) {
        List<string> labels = [];
        int origOffset = offset;
        bool jumped = false;

        for (int i = 0; i < 128; i++) {
            if (offset >= data.Length) {
                break;
            }
            byte len = data[offset];

            if (len == 0) {
                offset += 1;
                break;
            }

            if ((len & 0xC0) == 0xC0) {
                if (offset + 1 >= data.Length) {
                    throw new ArgumentException("Truncated pointer");
                }
                int ptr = ((len & 0x3F) << 8) | data[offset + 1];
                if (!jumped) {
                    origOffset = offset + 2;
                }
                offset = ptr;
                jumped = true;
                continue;
            }

            offset += 1;
            if (offset + len > data.Length) {
                throw new ArgumentException("Truncated label");
            }
            labels.Add(Encoding.UTF8.GetString(data, offset, len));
            offset += len;
        }

        return (string.Join(".", labels) + ".", jumped ? origOffset : offset);
    }
}

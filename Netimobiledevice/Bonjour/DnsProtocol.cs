using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace Netimobiledevice.Bonjour;

public static class DnsProtocol {
    public static byte[] EncodeName(string name) {
        name = name.TrimEnd('.');

        using MemoryStream stream = new MemoryStream();

        if (!string.IsNullOrEmpty(name)) {
            foreach (string label in name.Split('.')) {
                byte[] bytes = Encoding.UTF8.GetBytes(label);

                if (bytes.Length > 63) {
                    throw new ArgumentException("label too long", nameof(name));
                }

                stream.WriteByte((byte) bytes.Length);
                stream.Write(bytes);
            }
        }

        stream.WriteByte(0);
        return stream.ToArray();
    }

    public static (string Name, int Offset) DecodeName(
        ReadOnlySpan<byte> data,
        int offset) {
        List<string> labels = [];
        bool jumped = false;
        int originalEnd = offset;

        for (int iteration = 0; iteration < 128; iteration++) {
            if (offset >= data.Length) {
                break;
            }

            byte length = data[offset];

            if (length == 0) {
                offset++;
                break;
            }

            if ((length & 0xC0) == 0xC0) {
                if (offset + 1 >= data.Length) {
                    throw new InvalidDataException(
                        "truncated name pointer");
                }

                int ptr =
                    ((length & 0x3F) << 8) |
                    data[offset + 1];

                if (ptr >= data.Length) {
                    throw new InvalidDataException(
                        "bad name pointer");
                }

                if (!jumped) {
                    originalEnd = offset + 2;
                }

                offset = ptr;
                jumped = true;
                continue;
            }

            offset++;

            int end = offset + length;

            if (end > data.Length) {
                throw new InvalidDataException(
                    "truncated label");
            }

            labels.Add(
                Encoding.UTF8.GetString(data[offset..end]));

            offset = end;
        }

        return ($"{string.Join(".", labels)}.", jumped ? originalEnd : offset);
    }

    public static byte[] BuildQuery(
        string name,
        ushort qType,
        bool unicast = false) {
        using MemoryStream stream = new MemoryStream();

        WriteUInt16(stream, 0); // transaction ID
        WriteUInt16(stream, 0); // flags
        WriteUInt16(stream, 1); // QDCOUNT
        WriteUInt16(stream, 0); // ANCOUNT
        WriteUInt16(stream, 0); // NSCOUNT
        WriteUInt16(stream, 0); // ARCOUNT

        stream.Write(EncodeName(name));

        ushort qClass =
            (ushort) (MdnsConstants.ClassIn |
                     (unicast ? MdnsConstants.ClassQu : 0));

        WriteUInt16(stream, qType);
        WriteUInt16(stream, qClass);

        return stream.ToArray();
    }

    public static DnsRecord ParseRecord(
        ReadOnlySpan<byte> data,
        ref int offset) {
        (string Name, int Offset) decoded = DecodeName(data, offset);
        string name = decoded.Name;
        offset = decoded.Offset;

        if (offset + 10 > data.Length) {
            throw new InvalidDataException("truncated RR header");
        }

        ushort type = ReadUInt16(data, ref offset);
        ushort rClass = ReadUInt16(data, ref offset);
        uint ttl = ReadUInt32(data, ref offset);
        ushort rdLength = ReadUInt16(data, ref offset);

        if (offset + rdLength > data.Length) {
            throw new InvalidDataException("truncated RDATA");
        }

        int rdataStart = offset;
        ReadOnlySpan<byte> rdata = data.Slice(offset, rdLength);
        offset += rdLength;

        DnsRecord record = new DnsRecord {
            Name = name,
            Type = type,
            Class = (ushort) (rClass & 0x7FFF),
            Ttl = ttl
        };

        switch (type) {
            case MdnsConstants.QTypePtr: {
                (string Name, _) = DecodeName(data, rdataStart);
                record = record with {
                    PtrdName = Name
                };
                break;
            }

            case MdnsConstants.QTypeSrv when rdLength >= 6: {
                ushort priority =
                    BinaryPrimitives.ReadUInt16BigEndian(rdata[..2]);

                ushort weight =
                    BinaryPrimitives.ReadUInt16BigEndian(rdata.Slice(2, 2));

                ushort port =
                    BinaryPrimitives.ReadUInt16BigEndian(rdata.Slice(4, 2));


                (string Name, _) = DecodeName(data, rdataStart + 6);
                record = record with {
                    Priority = priority,
                    Weight = weight,
                    Port = port,
                    Target = Name
                };
                break;
            }

            case MdnsConstants.QTypeTxt: {
                Dictionary<string, string> properties = new Dictionary<string, string>(
                    StringComparer.Ordinal);

                int i = 0;

                while (i < rdata.Length) {
                    byte length = rdata[i++];

                    if (i + length > rdata.Length) {
                        break;
                    }

                    ReadOnlySpan<byte> segment = rdata.Slice(i, length);
                    i += length;

                    if (segment.Length == 0) {
                        continue;
                    }

                    int equals = segment.IndexOf((byte) '=');

                    if (equals >= 0) {
                        string key = Encoding.UTF8.GetString(
                            segment[..equals]);

                        string value = Encoding.UTF8.GetString(
                            segment[(equals + 1)..]);

                        properties[key] = value;
                    }
                    else {
                        string key = Encoding.UTF8.GetString(segment);
                        properties[key] = "";
                    }
                }

                record = record with {
                    Txt = properties
                };

                break;
            }

            case MdnsConstants.QTypeA when rdLength == 4:
                record = record with {
                    Address = new IPAddress(rdata).ToString()
                };
                break;

            case MdnsConstants.QTypeAaaa when rdLength == 16:
                record = record with {
                    Address = new IPAddress(rdata).ToString()
                };
                break;

            default:
                record = record with {
                    Raw = rdata.ToArray()
                };
                break;
        }

        return record;
    }

    public static List<DnsRecord> ParseMdnsMessage(
        ReadOnlySpan<byte> data) {
        List<DnsRecord> records = [];

        if (data.Length < 12) {
            return records;
        }

        int offset = 0;

        _ = ReadUInt16(data, ref offset); // ID
        _ = ReadUInt16(data, ref offset); // flags

        ushort qd = ReadUInt16(data, ref offset);
        ushort an = ReadUInt16(data, ref offset);
        ushort ns = ReadUInt16(data, ref offset);
        ushort ar = ReadUInt16(data, ref offset);

        for (int i = 0; i < qd; i++) {
            (_, int Offset) = DecodeName(data, offset);
            offset = Offset;

            if (offset + 4 > data.Length) {
                throw new InvalidDataException(
                    "truncated question");
            }

            offset += 4;
        }

        int totalRecords = an + ns + ar;

        for (int i = 0; i < totalRecords; i++) {
            records.Add(ParseRecord(data, ref offset));
        }

        return records;
    }

    public static List<(string Name, ushort QType)> ParseQuestions(
        ReadOnlySpan<byte> data) {
        List<(string Name, ushort QType)> questions = [];

        if (data.Length < 12) {
            return questions;
        }

        int offset = 0;

        _ = ReadUInt16(data, ref offset);
        _ = ReadUInt16(data, ref offset);

        ushort qd = ReadUInt16(data, ref offset);

        offset += 6; // AN, NS, AR

        for (int i = 0; i < qd; i++) {
            try {
                (string Name, int Offset) = DecodeName(data, offset);
                offset = Offset;

                if (offset + 4 > data.Length) {
                    break;
                }

                ushort qType = ReadUInt16(data, ref offset);
                _ = ReadUInt16(data, ref offset);

                questions.Add((Name, qType));
            }
            catch (InvalidDataException) {
                break;
            }
        }

        return questions;
    }

    public static byte[] BuildRecord(
        string name,
        ushort type,
        byte[] rdata,
        uint ttl,
        bool cacheFlush) {
        using MemoryStream stream = new MemoryStream();

        ushort rClass =
            (ushort) (MdnsConstants.ClassIn |
                     (cacheFlush ? 0x8000 : 0));

        stream.Write(EncodeName(name));
        WriteUInt16(stream, type);
        WriteUInt16(stream, rClass);
        WriteUInt32(stream, ttl);
        WriteUInt16(stream, checked((ushort) rdata.Length));
        stream.Write(rdata);

        return stream.ToArray();
    }

    public static byte[] EncodeTxt(
        IReadOnlyDictionary<string, string> properties) {
        if (properties.Count == 0) {
            return [0];
        }

        using MemoryStream stream = new MemoryStream();

        foreach (KeyValuePair<string, string> pair in properties) {
            byte[] segment =
                Encoding.UTF8.GetBytes($"{pair.Key}={pair.Value}");

            if (segment.Length > 255) {
                throw new ArgumentException(
                    $"TXT record entry too long: {pair.Key}");
            }

            stream.WriteByte((byte) segment.Length);
            stream.Write(segment);
        }

        return stream.ToArray();
    }

    public static byte[] EncodeSrv(
        ushort priority,
        ushort weight,
        ushort port,
        string target) {
        using MemoryStream stream = new MemoryStream();

        WriteUInt16(stream, priority);
        WriteUInt16(stream, weight);
        WriteUInt16(stream, port);

        stream.Write(EncodeName(target));

        return stream.ToArray();
    }

    private static ushort ReadUInt16(
        ReadOnlySpan<byte> data,
        ref int offset) {
        if (offset + 2 > data.Length) {
            throw new InvalidDataException("truncated uint16");
        }

        ushort value =
            BinaryPrimitives.ReadUInt16BigEndian(
                data.Slice(offset, 2));

        offset += 2;
        return value;
    }

    private static uint ReadUInt32(
        ReadOnlySpan<byte> data,
        ref int offset) {
        if (offset + 4 > data.Length) {
            throw new InvalidDataException("truncated uint32");
        }

        uint value =
            BinaryPrimitives.ReadUInt32BigEndian(
                data.Slice(offset, 4));

        offset += 4;
        return value;
    }

    private static void WriteUInt16(
        Stream stream,
        ushort value) {
        Span<byte> buffer = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(buffer, value);
        stream.Write(buffer);
    }

    private static void WriteUInt32(
        Stream stream,
        uint value) {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
        stream.Write(buffer);
    }
}

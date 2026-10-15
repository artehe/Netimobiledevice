using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Netimobiledevice.Services.Afc;

/// <summary>Wire-format helpers</summary>
internal static class AfcProtocol {
    public const int HeaderSize = 40;

    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true
    );

    internal static ReadOnlySpan<byte> Magic => "CFA6LPAA"u8;

    public static byte[] Concat(params ReadOnlySpan<byte[]> parts) {
        int total = 0;
        foreach (byte[] p in parts) {
            total += p.Length;
        }

        byte[] result = new byte[total];
        int offset = 0;
        foreach (byte[] p in parts) {
            p.CopyTo(result, offset);
            offset += p.Length;
        }

        return result;
    }

    public static byte[] BuildPacket(
        ulong packetNum,
        AfcOpCode operation,
        ReadOnlySpan<byte> payload,
        ulong thisLength = 0
    ) {
        ulong entireLength = (ulong) (HeaderSize + payload.Length);

        byte[] packet = new byte[HeaderSize + payload.Length];
        Span<byte> span = packet.AsSpan();

        Magic.CopyTo(span);
        BinaryPrimitives.WriteUInt64LittleEndian(span[8..], entireLength);
        BinaryPrimitives.WriteUInt64LittleEndian(span[16..], thisLength == 0 ? entireLength : thisLength);
        BinaryPrimitives.WriteUInt64LittleEndian(span[24..], packetNum);
        BinaryPrimitives.WriteUInt64LittleEndian(span[32..], (ulong) operation);
        payload.CopyTo(span[HeaderSize..]);

        return packet;
    }

    public static byte[] I64(long value) {
        byte[] b = new byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(b, value);
        return b;
    }

    /// <summary>
    /// Convert a NUL-terminated key/value list (<c>key\0value\0key\0value\0</c>) to a dictionary.
    /// </summary>
    public static Dictionary<string, string> ListToDict(ReadOnlySpan<byte> raw) {
        string[] parts = StrictUtf8.GetString(raw).Split('\0');

        // Drop what follows the final terminator
        int count = parts.Length - 1;
        if (count % 2 != 0) {
            throw new FormatException("AFC list is not key/value aligned");
        }

        Dictionary<string, string> result = new Dictionary<string, string>(count / 2);
        for (int i = 0; i < count; i += 2) {
            result[parts[i]] = parts[i + 1];
        }

        return result;
    }


    /// <summary>Every NULL-terminated string in the buffer.</summary>
    public static List<string> ParseCStrings(ReadOnlySpan<byte> data) {
        List<string> result = [];
        while (true) {
            int nul = data.IndexOf((byte) 0);
            if (nul < 0) {
                break;
            }

            result.Add(StrictUtf8.GetString(data[..nul]));
            data = data[(nul + 1)..];
        }
        return result;
    }

    public static AfcHeader ParseHeader(ReadOnlySpan<byte> data) {
        if (data.Length < HeaderSize) {
            throw new AfcException("Truncated AFC header");
        }

        if (!data[..8].SequenceEqual(Magic)) {
            throw new AfcException("Invalid AFC magic");
        }

        return new AfcHeader(
            BinaryPrimitives.ReadUInt64LittleEndian(data[8..]),
            BinaryPrimitives.ReadUInt64LittleEndian(data[16..]),
            BinaryPrimitives.ReadUInt64LittleEndian(data[24..]),
            (AfcOpCode) BinaryPrimitives.ReadUInt64LittleEndian(data[32..])
        );
    }
    public static long ParseLong(string value) => long.Parse(
        value,
        NumberStyles.Integer,
        CultureInfo.InvariantCulture
    );

    public static AfcError ParseStatus(ReadOnlySpan<byte> data) =>
        data.Length >= 8
            ? (AfcError) BinaryPrimitives.ReadUInt64LittleEndian(data)
            : throw new AfcException("Truncated AFC status payload");

    public static byte[] U64(ulong value) {
        byte[] b = new byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(b, value);
        return b;
    }
}

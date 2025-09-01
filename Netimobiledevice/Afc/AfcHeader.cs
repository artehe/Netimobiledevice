using System;

namespace Netimobiledevice.Afc;

internal class AfcHeader
{
    public static byte[] MAGIC { get; } = "CFA6LPAA"u8.ToArray();

    public ulong EntireLength { get; set; }
    public ulong Length { get; set; }
    public ulong PacketNumber { get; set; }
    public AfcOpCode Operation { get; set; }

    public static AfcHeader FromBytes(ReadOnlySpan<byte> bytes)
    {
        ReadOnlySpan<byte> readMagicBytes = bytes[..MAGIC.Length];
        if (!readMagicBytes.SequenceEqual(MAGIC)) {
            throw new AfcException("Missmatch in magic bytes for afc header");
        }

        AfcHeader afcHeader = new AfcHeader() {
            EntireLength = BitConverter.ToUInt64(bytes[8..]),
            Length = BitConverter.ToUInt64(bytes[16..]),
            PacketNumber = BitConverter.ToUInt64(bytes[24..]),
            Operation = (AfcOpCode) BitConverter.ToUInt64(bytes[32..]),
        };
        return afcHeader;
    }

    public byte[] GetBytes() =>
        [
            .. MAGIC,
            .. BitConverter.GetBytes(EntireLength),
            .. BitConverter.GetBytes(Length),
            .. BitConverter.GetBytes(PacketNumber),
            .. BitConverter.GetBytes((ulong) Operation),
        ];

    public static int GetSize() => MAGIC.Length + (sizeof(ulong) * 4);
}

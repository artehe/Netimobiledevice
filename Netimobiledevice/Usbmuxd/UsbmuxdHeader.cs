using System;
using System.Buffers.Binary;

namespace Netimobiledevice.Usbmuxd;

internal struct UsbmuxdHeader {
    /// <summary>
    /// The size of UsbmuxdHeader in bytes
    /// </summary>
    public const int SIZE = sizeof(int) + sizeof(UsbmuxdVersion) + sizeof(UsbmuxdMessageType) + sizeof(int);

    /// <summary>
    /// Length of message including header
    /// </summary>
    public int Length;
    /// <summary>
    /// Protocol version
    /// </summary>
    public UsbmuxdVersion Version;
    /// <summary>
    /// Message type
    /// </summary>
    public UsbmuxdMessageType Message;
    /// <summary>
    /// Responses to this query will echo back this tag
    /// </summary>
    public int Tag;

    private readonly void WriteBytes(Span<byte> destination) {
        BinaryPrimitives.WriteInt32LittleEndian(destination[0..4], Length);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[4..8], (uint) Version);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[8..12], (uint) Message);
        BinaryPrimitives.WriteInt32LittleEndian(destination[12..16], Tag);
    }

    public readonly byte[] GetBytes() {
        byte[] buffer = new byte[SIZE];
        WriteBytes(buffer);
        return buffer;
    }

    public static UsbmuxdHeader FromBytes(ReadOnlySpan<byte> bytes) {
        if (bytes.Length < SIZE) {
            throw new ArgumentException($"Expected at least {SIZE} bytes, got {bytes.Length}", nameof(bytes));
        }
        return new UsbmuxdHeader {
            Length = BinaryPrimitives.ReadInt32LittleEndian(bytes[0..4]),
            Version = (UsbmuxdVersion) BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..8]),
            Message = (UsbmuxdMessageType) BinaryPrimitives.ReadUInt32LittleEndian(bytes[8..12]),
            Tag = BinaryPrimitives.ReadInt32LittleEndian(bytes[12..16]),
        };
    }
}

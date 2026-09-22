using System;

namespace Netimobiledevice.Remoted.Xpc;

/// <summary>
/// An XPC shared-memory region descriptor: a 4-byte length followed by an
/// unused/reserved 4-byte field, matching upstream's
/// <c>Struct("length" / Int32ul, Int32ul)</c>.
/// </summary>
public class XpcShmem(int length, int reserved = 0) : XpcObject<int>(length)
{
    public int Reserved { get; set; } = reserved;

    public override bool IsAligned => false;

    public override bool IsPrefixed => false;

    public override XpcMessageType Type => XpcMessageType.Shmem;

    public static XpcShmem Deserialise(byte[] data)
    {
        int length = BitConverter.ToInt32(data, 0);
        int reserved = BitConverter.ToInt32(data, 4);
        return new XpcShmem(length, reserved);
    }

    public override byte[] Serialise()
    {
        return [
            .. BitConverter.GetBytes(Data),
            .. BitConverter.GetBytes(Reserved)
        ];
    }
}

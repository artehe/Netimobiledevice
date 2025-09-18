using System;

namespace Netimobiledevice.Afc.Packets;

internal class AfcFileWritePacket(ulong handle, byte[]? data = null) : AfcPacket
{
    public ulong Handle { get; set; } = handle;
    public byte[] Data { get; set; } = data ?? [];

    public override int DataSize => sizeof(ulong) + Data.Length;

    public override byte[] GetBytes()
    {
        return [
            .. Header.GetBytes(),
            .. BitConverter.GetBytes(Handle),
            .. Data
        ];
    }
}

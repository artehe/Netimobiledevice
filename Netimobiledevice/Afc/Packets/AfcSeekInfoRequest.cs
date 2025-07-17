using System;

namespace Netimobiledevice.Afc.Packets
{
    internal class AfcSeekInfoRequest(ulong handle, ulong whence, long offset) : AfcPacket
    {
        public ulong Handle { get; set; } = handle;
        public ulong Whence { get; set; } = whence;
        public long Offset { get; set; } = offset;

        public override int DataSize => (sizeof(ulong) * 2) + sizeof(long);

        public override byte[] GetBytes()
        {
            return [
                .. Header.GetBytes(),
                .. BitConverter.GetBytes(Handle),
                .. BitConverter.GetBytes(Whence),
                .. BitConverter.GetBytes(Offset),
            ];
        }
    }
}

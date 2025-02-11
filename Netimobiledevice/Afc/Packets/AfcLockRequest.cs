using System;

namespace Netimobiledevice.Afc.Packets
{
    internal class AfcLockRequest(ulong handle, ulong op) : AfcPacket
    {
        public ulong Handle { get; set; } = handle;
        public ulong Op { get; set; } = op;

        public override int DataSize => sizeof(ulong) * 2;

        public override byte[] GetBytes()
        {
            return [
                .. Header.GetBytes(),
                .. BitConverter.GetBytes(Handle),
                .. BitConverter.GetBytes(Op)
            ];
        }
    }
}

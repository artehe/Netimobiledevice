using System;

namespace Netimobiledevice.Afc.Packets
{
    internal class AfcFileReadRequest : AfcPacket
    {
        public ulong Handle { get; set; }
        public ulong Size { get; set; }

        public override int DataSize => sizeof(ulong) * 2;

        public override byte[] GetBytes()
        {
            return [
                .. Header.GetBytes(),
                .. BitConverter.GetBytes(Handle),
                .. BitConverter.GetBytes(Size)
            ];
        }
    }
}

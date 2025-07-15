using System;

namespace Netimobiledevice.Afc.Packets
{
    internal class AfcTellRequest(ulong handle) : AfcPacket
    {
        public ulong Handle { get; set; } = handle;

        public override int DataSize => sizeof(ulong);

        public override byte[] GetBytes()
        {
            return [
                .. Header.GetBytes(),
                .. BitConverter.GetBytes(Handle)
            ];
        }
    }
}

using System;

namespace Netimobiledevice.Afc.Packets
{
    internal class AfcFileCloseRequest(ulong handle) : AfcPacket
    {
        public ulong Handle = handle;

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

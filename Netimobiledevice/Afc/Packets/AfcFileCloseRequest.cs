using System;

namespace Netimobiledevice.Afc.Packets
{
    internal class AfcFileCloseRequest : AfcPacket
    {
        public ulong Handle;

        public override int DataSize => sizeof(ulong);

        public override byte[] GetBytes()
        {
            return BitConverter.GetBytes(Handle);
        }
    }
}

using System;
using System.Collections.Generic;

namespace Netimobiledevice.Afc.Packets
{
    internal class AfcFileCloseRequest : AfcPacket
    {
        public ulong Handle;

        public override int DataSize => sizeof(ulong);

        public override byte[] GetBytes()
        {
            List<byte> bytes = new List<byte>();
            bytes.AddRange(Header.GetBytes());
            bytes.AddRange(BitConverter.GetBytes(Handle));
            return bytes.ToArray();
        }
    }
}

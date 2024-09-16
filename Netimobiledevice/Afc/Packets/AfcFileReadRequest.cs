using System;
using System.Collections.Generic;

namespace Netimobiledevice.Afc.Packets
{
    internal class AfcFileReadRequest : AfcPacket
    {
        public ulong Handle { get; set; }
        public ulong Size { get; set; }

        public override int DataSize => sizeof(ulong) * 2;

        public override byte[] GetBytes()
        {
            List<byte> bytes = new List<byte>();
            bytes.AddRange(Header.GetBytes());
            bytes.AddRange(BitConverter.GetBytes(Handle));
            bytes.AddRange(BitConverter.GetBytes(Size));
            return bytes.ToArray();
        }
    }
}

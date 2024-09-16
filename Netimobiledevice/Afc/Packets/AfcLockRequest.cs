using System;
using System.Collections.Generic;

namespace Netimobiledevice.Afc.Packets
{
    internal class AfcLockRequest : AfcPacket
    {
        public ulong Handle { get; set; }
        public ulong Op { get; set; }

        public override int DataSize => sizeof(ulong) * 2;

        public override byte[] GetBytes()
        {
            List<byte> bytes = new List<byte>();
            bytes.AddRange(Header.GetBytes());
            bytes.AddRange(BitConverter.GetBytes(Handle));
            bytes.AddRange(BitConverter.GetBytes(Op));
            return bytes.ToArray();
        }
    }
}

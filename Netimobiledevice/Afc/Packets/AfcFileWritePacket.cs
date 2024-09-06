using System;
using System.Collections.Generic;

namespace Netimobiledevice.Afc.Packets
{
    internal class AfcFileWritePacket : AfcPacket
    {
        public ulong Handle { get; set; }
        public byte[] Data { get; set; } = Array.Empty<byte>();

        public override int DataSize => sizeof(ulong) + Data.Length;

        public override byte[] GetBytes()
        {
            List<byte> bytes = new List<byte>();
            bytes.AddRange(Header.GetBytes());
            bytes.AddRange(BitConverter.GetBytes(Handle));
            bytes.AddRange(Data);
            return bytes.ToArray();
        }
    }
}

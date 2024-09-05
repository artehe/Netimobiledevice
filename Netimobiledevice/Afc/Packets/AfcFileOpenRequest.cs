using System;
using System.Collections.Generic;

namespace Netimobiledevice.Afc.Packets
{
    internal class AfcFileOpenRequest : AfcPacket
    {
        public AfcFileOpenMode Mode { get; }
        public CString Filename { get; }

        public override int DataSize => sizeof(AfcFileOpenMode) + Filename.Length;

        public AfcFileOpenRequest(AfcFileOpenMode mode, string filename)
        {
            Mode = mode;
            Filename = new CString(filename);
        }

        public override byte[] GetBytes()
        {
            List<byte> bytes = new List<byte>();
            bytes.AddRange(BitConverter.GetBytes((ulong) Mode));
            bytes.AddRange(Filename.Bytes);
            return bytes.ToArray();
        }
    }
}

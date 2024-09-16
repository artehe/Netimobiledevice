using System.Collections.Generic;

namespace Netimobiledevice.Afc.Packets
{
    internal class AfcReadDirectoryRequest : AfcPacket
    {
        public CString Filename { get; }

        public override int DataSize => Filename.Length;

        public AfcReadDirectoryRequest(string filename)
        {
            Filename = new CString(filename);
        }

        public override byte[] GetBytes()
        {
            List<byte> bytes = new List<byte>();
            bytes.AddRange(Header.GetBytes());
            bytes.AddRange(Filename.Bytes);
            return bytes.ToArray();
        }
    }
}

using Netimobiledevice.Extentions;
using System.Collections.Generic;
using System.Text;

namespace Netimobiledevice.Afc
{
    internal class AfcReadDirectoryRequest
    {
        public CString Filename { get; }

        public AfcReadDirectoryRequest(string filename)
        {
            Filename = new CString(filename, Encoding.UTF8);
        }

        public byte[] GetBytes()
        {
            List<byte> bytes = new List<byte>();
            bytes.AddRange(Filename.Bytes);
            return bytes.ToArray();
        }
    }
}

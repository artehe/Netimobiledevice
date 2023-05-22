using System.Collections.Generic;
using System.Text;

namespace Netimobiledevice.Extentions
{
    internal class CString
    {
        public byte[] Bytes { get; }

        public int Length => Bytes.Length;

        public CString(string value, Encoding encoding)
        {
            List<byte> bytes = new List<byte>();
            bytes.AddRange(encoding.GetBytes(value));
            bytes.AddRange(encoding.GetBytes("\0"));
            Bytes = bytes.ToArray();
        }
    }
}

using System.Text;

namespace Netimobiledevice.Afc.Packets
{
    internal class CString
    {
        private readonly Encoding _encoding;

        public byte[] Bytes => _encoding.GetBytes($"{Value}\0");
        public string Value { get; set; }

        public int Length => Bytes.Length;

        public CString(string value, Encoding encoding)
        {
            Value = value;
            _encoding = encoding;
        }

        public CString(string value) : this(value, Encoding.UTF8) { }
    }
}

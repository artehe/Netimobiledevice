using System.Text;

namespace Netimobiledevice.Afc
{
    internal class CString(string str, Encoding encoding)
    {
        private readonly Encoding _encoding = encoding;

        public int Length => GetBytes().Length;

        public string SourceValue { get; } = str;

        public string Value => $"{SourceValue}\0";

        public byte[] GetBytes()
        {
            return _encoding.GetBytes(Value);
        }
    }
}

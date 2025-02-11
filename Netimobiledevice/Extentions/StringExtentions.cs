using Netimobiledevice.Afc;
using System.Text;

namespace Netimobiledevice.Extentions
{
    internal static class StringExtentions
    {
        public static CString AsCString(this string str, Encoding encoding)
        {
            return new CString(str, encoding);
        }
    }
}

using System.Text;

namespace Netimobiledevice.Extentions
{
    internal static class StringExtentions
    {
        public static string AsCString(this string str)
        {
            return $"{str}\0";
        }

        public static byte[] GetBytes(this string str, Encoding encoding)
        {
            return encoding.GetBytes(str);
        }
    }
}

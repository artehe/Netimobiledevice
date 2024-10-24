using System;

namespace Netimobiledevice.Extentions
{
    internal static class ByteArrayExtensions
    {
        public static byte[] EnsureBigEndian(this byte[] src)
        {
            if (BitConverter.IsLittleEndian) {
                Array.Reverse(src);
            }
            return src;
        }
    }
}

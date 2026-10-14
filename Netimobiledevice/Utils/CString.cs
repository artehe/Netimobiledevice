using System.Text;

namespace Netimobiledevice.Utils;

internal static class CString {

    /// <summary>UTF-8 bytes followed by a NULL terminator.</summary>
    public static byte[] GetBytes(string str) {
        int length = Encoding.UTF8.GetByteCount(str);
        byte[] b = new byte[length + 1];
        Encoding.UTF8.GetBytes(str, b);
        return b;
    }
}

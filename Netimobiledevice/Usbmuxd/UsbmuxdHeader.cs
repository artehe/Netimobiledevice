using System.Runtime.InteropServices;

namespace Netimobiledevice.Usbmuxd;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct UsbmuxdHeader
{
    public int Length; // Length of message including header
    public UsbmuxdVersion Version; // Protocol version
    public UsbmuxdMessageType Message; // Message type
    public int Tag; // Responses to this query will echo back this tag

    internal static UsbmuxdHeader FromBytes(byte[] arr)
    {
        UsbmuxdHeader str = new UsbmuxdHeader();

        int size = Marshal.SizeOf(str);
        IntPtr ptr = IntPtr.Zero;
        try {
            ptr = Marshal.AllocHGlobal(size);

            Marshal.Copy(arr, 0, ptr, size);

            str = (UsbmuxdHeader) Marshal.PtrToStructure(ptr, str.GetType());
        }
        finally {
            Marshal.FreeHGlobal(ptr);
        }
        return str;
    }
}

internal static class UsbmuxdHeaderExtentions
{
    public static byte[] GetBytes(this UsbmuxdHeader header)
    {
        int size = Marshal.SizeOf(header);
        byte[] arr = new byte[size];

        IntPtr ptr = IntPtr.Zero;
        try {
            ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(header, ptr, true);
            Marshal.Copy(ptr, arr, 0, size);
        }
        finally {
            Marshal.FreeHGlobal(ptr);
        }
        return arr;
    }
}

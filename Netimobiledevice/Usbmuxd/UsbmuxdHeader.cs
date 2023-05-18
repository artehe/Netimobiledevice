using System;
using System.Runtime.InteropServices;

namespace Netimobiledevice.Usbmuxd
{
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

                str = Marshal.PtrToStructure<UsbmuxdHeader>(ptr);
            }
            finally {
                Marshal.FreeHGlobal(ptr);
            }
            return str;
        }
    }
}

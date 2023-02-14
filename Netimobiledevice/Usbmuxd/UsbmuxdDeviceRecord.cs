using System;
using System.Runtime.InteropServices;

namespace Netimobiledevice.Usbmuxd
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct UsbmuxdDeviceRecord
    {
        public int DeviceId;
        public short ProductId;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string SerialNumber;
        public int Location;

        internal static UsbmuxdDeviceRecord FromBytes(byte[] arr)
        {
            UsbmuxdDeviceRecord str = new UsbmuxdDeviceRecord();

            int size = Marshal.SizeOf(str);
            IntPtr ptr = IntPtr.Zero;
            try {
                ptr = Marshal.AllocHGlobal(size);

                Marshal.Copy(arr, 0, ptr, size);

                str = (UsbmuxdDeviceRecord) Marshal.PtrToStructure(ptr, str.GetType());
            }
            finally {
                Marshal.FreeHGlobal(ptr);
            }
            return str;
        }
    }
}

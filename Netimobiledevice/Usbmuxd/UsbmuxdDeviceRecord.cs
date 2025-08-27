using System.Runtime.InteropServices;

namespace Netimobiledevice.Usbmuxd;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct UsbmuxdDeviceRecord
{
    public uint DeviceId;
    public short ProductId;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
    public string SerialNumber;
    public int Location;
}

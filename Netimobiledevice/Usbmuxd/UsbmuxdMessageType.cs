namespace Netimobiledevice.Usbmuxd;

internal enum UsbmuxdMessageType : uint
{
    Result = 1,
    Connect = 2,
    Listen = 3,
    Add = 4,
    Remove = 5,
    Paired = 6,
    Plist = 8
}

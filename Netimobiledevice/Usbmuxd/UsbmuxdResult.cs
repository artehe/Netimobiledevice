namespace Netimobiledevice.Usbmuxd;

internal enum UsbmuxdResult
{
    UnknownError = -1,
    Ok = 0,
    BadCommand = 1,
    BadDevice = 2,
    ConnectionRefused = 3,
    BadVersion = 6
}

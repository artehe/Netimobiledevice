namespace Netimobiledevice.Usbmuxd;

public static class Usbmux
{
    /// <summary>
    /// Contacts usbmuxd and retrieves a list of connected devices.
    /// </summary>
    /// <returns>
    /// A list of connected Usbmux devices
    /// </returns>
    public static List<UsbmuxdDevice> GetDeviceList()
    {
        var muxConnection = UsbmuxConnection.Create();
        muxConnection.UpdateDeviceList(100);
        List<UsbmuxdDevice> devices = muxConnection.Devices;
        muxConnection.Close();
        return devices;
    }
}

using Netimobiledevice.Usbmuxd;

namespace NetimobiledeviceDemo;

public class Program
{
    internal static void Main(string[] args)
    {
        List<UsbmuxdDevice> devices = Usbmux.GetDeviceList();
        Console.WriteLine($"There's {devices.Count} devices connected");
        foreach (UsbmuxdDevice device in devices) {
            Console.WriteLine($"Device found: {device.DeviceId} - {device.Serial}");
        }
    }
}

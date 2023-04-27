using Netimobiledevice.Lockdown;
using Netimobiledevice.Lockdown.Services;
using Netimobiledevice.Plist;
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

        Usbmux.Subscribe(SubscriptionCallback);
        Usbmux.Unsubscribe();

        LockdownClient lockdown = new LockdownClient();
        SpringBoardServicesService springBoard = new SpringBoardServicesService(lockdown);
        PropertyNode png = springBoard.GetIconPNGData("net.whatsapp.WhatsApp");

        Console.ReadLine();
    }

    private static void SubscriptionCallback(UsbmuxdDevice device, UsbmuxdConnectionEventType connectionEvent)
    {
        Console.WriteLine("NewCallbackExecuted");
        Console.WriteLine($"Connection event: {connectionEvent}");
        Console.WriteLine($"Device: {device.DeviceId} - {device.Serial}");
    }
}

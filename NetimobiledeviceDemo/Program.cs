using Netimobiledevice.Lockdown;
using Netimobiledevice.Lockdown.Services;
using Netimobiledevice.Plist;
using Netimobiledevice.Usbmuxd;

namespace NetimobiledeviceDemo;

public class Program
{
    internal static async Task Main(string[] args)
    {
        List<UsbmuxdDevice> devices = Usbmux.GetDeviceList();
        Console.WriteLine($"There's {devices.Count} devices connected");
        foreach (UsbmuxdDevice device in devices) {
            Console.WriteLine($"Device found: {device.DeviceId} - {device.Serial}");
        }

        Usbmux.Subscribe(SubscriptionCallback);
        Usbmux.Unsubscribe();

        LockdownClient lockdown = new LockdownClient();

        InstallationProxyService installationProxyService = new InstallationProxyService(lockdown);
        ArrayNode apps = await installationProxyService.Browse();

        /*
        SpringBoardServicesService springBoard = new SpringBoardServicesService(lockdown);
        PropertyNode png = springBoard.GetIconPNGData("net.whatsapp.WhatsApp");
        */

        OsTraceService osTraceService = new OsTraceService(lockdown);
        DictionaryNode pidList = await osTraceService.GetPidList();

        DiagnosticsService diagnosticsService = new DiagnosticsService(lockdown);
        DictionaryNode info = diagnosticsService.GetBattery();

        /*
        SyslogService syslog = new SyslogService(lockdown);
        foreach (string line in syslog.Watch()) {
            Console.WriteLine(line);
        }
        */

        Console.ReadLine();
    }

    private static void SubscriptionCallback(UsbmuxdDevice device, UsbmuxdConnectionEventType connectionEvent)
    {
        Console.WriteLine("NewCallbackExecuted");
        Console.WriteLine($"Connection event: {connectionEvent}");
        Console.WriteLine($"Device: {device.DeviceId} - {device.Serial}");
    }
}

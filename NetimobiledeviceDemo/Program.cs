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
        UsbmuxdDevice? testDevice = null;
        foreach (UsbmuxdDevice device in devices) {
            Console.WriteLine($"Device found: {device.DeviceId} - {device.Serial}");
            if (device.ConnectionType == UsbmuxdConnectionType.Usb) {
                testDevice = device;
            }
        }

        Usbmux.Subscribe(SubscriptionCallback, SubscriptionErrorCallback);

        using (LockdownClient lockdown = LockdownClient.CreateLockdownClient(testDevice?.Serial ?? string.Empty)) {

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

            Mobilebackup2Service mobilebackup2Service = new Mobilebackup2Service(lockdown);
            await mobilebackup2Service.Backup();

            /*
            SyslogService syslog = new SyslogService(lockdown);
            foreach (string line in syslog.Watch()) {
                Console.WriteLine(line);
            }
            */
        }

        Usbmux.Unsubscribe();

        Console.ReadLine();
    }

    private static void SubscriptionCallback(UsbmuxdDevice device, UsbmuxdConnectionEventType connectionEvent)
    {
        Console.WriteLine("NewCallbackExecuted");
        Console.WriteLine($"Connection event: {connectionEvent}");
        Console.WriteLine($"Device: {device.DeviceId} - {device.Serial}");
    }

    private static void SubscriptionErrorCallback(Exception ex)
    {
        Console.WriteLine("NewErrorCallbackExecuted");
        Console.WriteLine(ex.Message);
    }
}

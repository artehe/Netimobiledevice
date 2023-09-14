using Netimobiledevice.Backup;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Lockdown.Services;
using Netimobiledevice.Misagent;
using Netimobiledevice.NotificationProxy;
using Netimobiledevice.Plist;
using Netimobiledevice.SpringBoardServices;
using Netimobiledevice.Usbmuxd;

namespace NetimobiledeviceDemo;

public class Program
{
    internal static async Task Main()
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
        Usbmux.Unsubscribe();

        using (LockdownClient lockdown = LockdownClient.CreateLockdownClient(testDevice?.Serial ?? string.Empty, false)) {
            using (NotificationProxyService np = new NotificationProxyService(lockdown)) {
                np.ReceivedNotification += Np_ReceivedNotification;
                foreach (ReceivableNotification notification in Enum.GetValues(typeof(ReceivableNotification))) {
                    np.ObserveNotification(notification);
                }

                using (MisagentService misagentService = new MisagentService(lockdown)) {
                    misagentService.GetInstalledProvisioningProfiles();
                }

                using (DeviceBackup backupJob = new DeviceBackup(lockdown, @"%appdata%\..\Local\Temp")) {
                    await backupJob.Start();
                }

                using (InstallationProxyService installationProxyService = new InstallationProxyService(lockdown)) {
                    ArrayNode apps = await installationProxyService.Browse();
                }

                using (SpringBoardServicesService springBoard = new SpringBoardServicesService(lockdown)) {
                    PropertyNode png = springBoard.GetIconPNGData("net.whatsapp.WhatsApp");
                }

                using (DiagnosticsService diagnosticsService = new DiagnosticsService(lockdown)) {
                    DictionaryNode info = diagnosticsService.GetBattery();
                }

                /*
                SyslogService syslog = new SyslogService(lockdown);
                foreach (string line in syslog.Watch()) {
                    Console.WriteLine(line);
                }
                */

                Console.ReadLine();
            }
        }
    }

    private static void Np_ReceivedNotification(object? sender, ReceivedNotificationEventArgs e)
    {
        throw new NotImplementedException();
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

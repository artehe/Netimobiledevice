using Netimobiledevice.Backup;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Lockdown.Services;
using Netimobiledevice.Misagent;
using Netimobiledevice.Mobilesync;
using Netimobiledevice.Plist;
using Netimobiledevice.SpringBoardServices;
using Netimobiledevice.Usbmuxd;

namespace NetimobiledeviceDemo;

public class Program
{
    internal static async Task Main()
    {
        TaskScheduler.UnobservedTaskException += (sender, args) => {
            Console.WriteLine($"UnobservedTaskException error: {args.Exception}");
        };

        List<UsbmuxdDevice> devices = Usbmux.GetDeviceList();
        Console.WriteLine($"There's {devices.Count} devices connected");
        UsbmuxdDevice? testDevice = null;
        foreach (UsbmuxdDevice device in devices) {
            Console.WriteLine($"Device found: {device.DeviceId} - {device.Serial}");
            if (device.ConnectionType == UsbmuxdConnectionType.Usb) {
                testDevice = device;
            }
        }

        using (LockdownClient lockdown = LockdownClient.CreateLockdownClient(testDevice?.Serial ?? string.Empty)) {
            PropertyNode? val = lockdown.GetValue("com.apple.mobile.tethered_sync", null);
            DictionaryNode tetherValue = new DictionaryNode() {
                { "DisableTethered", new BooleanNode(false) },
                { "SyncingOS", new StringNode("Windows") }
            };
            lockdown.SetValue("com.apple.mobile.tethered_sync", "Calendars", tetherValue);
            val = lockdown.GetValue("com.apple.mobile.tethered_sync", null);

            using (MobilesyncService mobilesyncService = await MobilesyncService.StartServiceAsync(lockdown)) {
                string anchor = new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc).ToString();
                MobilesyncAnchors anchors = new MobilesyncAnchors() {
                    DeviceAnchor = null,
                    ComputerAnchor = anchor
                };
                string mobilesyncdataPath = "mobileSyncedData.plist";
                await mobilesyncService.StartSync("com.apple.Contacts", anchors);
                mobilesyncService.GetAllRecordsFromDevice();
                ArrayNode entities = new ArrayNode();
                await foreach (PropertyNode entity in mobilesyncService.ReceiveChanges(null)) {
                    entities.Add(entity);
                    mobilesyncService.AcknowledgeChangesFromDevice();
                }
                byte[] fileData = PropertyList.SaveAsByteArray(entities, PlistFormat.Xml);
                File.WriteAllBytes(mobilesyncdataPath, fileData);
                await mobilesyncService.FinishSync();
            }
        }

        Usbmux.Subscribe(SubscriptionCallback, SubscriptionErrorCallback);
        Usbmux.Unsubscribe();

        using (LockdownClient lockdown = LockdownClient.CreateLockdownClient(testDevice?.Serial ?? string.Empty)) {
            Progress<PairingState> progress = new();
            progress.ProgressChanged += Progress_ProgressChanged;
            if (!lockdown.IsPaired) {
                await lockdown.PairAsync(progress);
            }
        }

        await Task.Delay(1000);

        using (LockdownClient lockdown = LockdownClient.CreateLockdownClient(testDevice?.Serial ?? string.Empty)) {
            string path = "backups";
            if (Directory.Exists(path)) {
                Directory.Delete(path, true);
            }
            using (DeviceBackup backupJob = new DeviceBackup(lockdown, path)) {
                await backupJob.Start();
            }
        }

        using (LockdownClient lockdown = LockdownClient.CreateLockdownClient(testDevice?.Serial ?? string.Empty)) {
            using (MisagentService misagentService = new MisagentService(lockdown)) {
                await misagentService.GetInstalledProvisioningProfiles();
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

            using (SyslogService syslog = new SyslogService(lockdown)) {
                foreach (string line in syslog.Watch()) {
                    Console.WriteLine(line);
                }
            }
        }

        Console.ReadLine();
    }

    private static void Progress_ProgressChanged(object? sender, PairingState e)
    {
        Console.WriteLine($"Pair Progress Changed: {e}");
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

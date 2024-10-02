using Microsoft.Extensions.Logging;
using Netimobiledevice;
using Netimobiledevice.Backup;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Lockdown.Pairing;
using Netimobiledevice.Usbmuxd;

namespace NetimobiledeviceDemo;

public class Program
{
    private static readonly CancellationTokenSource tokenSource = new CancellationTokenSource();

    internal static async Task Main()
    {
        using ILoggerFactory factory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug).AddConsole());
        ILogger logger = factory.CreateLogger("NetimobiledeviceDemo");
        logger.LogInformation("Hello World! Logging is {Description}.", "fun");

        TaskScheduler.UnobservedTaskException += (sender, args) => {
            Console.WriteLine($"UnobservedTaskException error: {args.Exception}");
        };

        Console.CancelKeyPress += (sender, eventArgs) => {
            Console.WriteLine("Cancellation requested...");
            tokenSource.Cancel();
            // Prevent the process from terminating immediately
            eventArgs.Cancel = true;
        };
        Console.WriteLine("Press Ctrl+C to cancel the operation.");

        List<UsbmuxdDevice> devices = Usbmux.GetDeviceList();

        if (!devices.Any()) {
            logger.LogError("No device is connected to the system.");
            return;
        }

        logger.LogDebug("There's {deviceCount} devices connected", devices.Count);
        foreach (UsbmuxdDevice device in devices) {
            Console.WriteLine($"Device found: {device.DeviceId} - {device.Serial}");
        }

        using (LockdownClient lockdown = MobileDevice.CreateUsingUsbmux(logger: logger)) {
            Progress<PairingState> progress = new();
            progress.ProgressChanged += Progress_ProgressChanged;
            if (!lockdown.IsPaired) {
                await lockdown.PairAsync(progress);
            }
        }

        using (LockdownClient lockdown = MobileDevice.CreateUsingUsbmux(logger: logger)) {
            string path = "backups";
            using (DeviceBackup backupJob = new DeviceBackup(lockdown, path)) {
                await backupJob.Start(tokenSource.Token);
            }
        }
        Console.WriteLine($"Backup done!");
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

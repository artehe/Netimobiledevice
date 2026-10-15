using Microsoft.Extensions.Logging;
using Netimobiledevice;
using Netimobiledevice.Backup;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Lockdown.Pairing;
using Netimobiledevice.NotificationProxy;
using Netimobiledevice.Plist;
using Netimobiledevice.Remoted;
using Netimobiledevice.Remoted.Tunnel;
using Netimobiledevice.Services;
using Netimobiledevice.Services.Springboard;
using Netimobiledevice.Usbmuxd;
using System.ComponentModel;

namespace NetimobiledeviceDemo;

public class Program {
    private static readonly CancellationTokenSource tokenSource = new CancellationTokenSource();

    internal static async Task Main() {
        using ILoggerFactory factory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug).AddConsole());
        ILogger logger = factory.CreateLogger("NetimobiledeviceDemo");
        logger.LogInformation("Hello World! Logging is fun.");

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

        List<UsbmuxdDevice> devices = await Usbmux.GetDeviceListAsync(cancellationToken: tokenSource.Token);
        if (devices.Count == 0) {
            logger.LogError("No device is connected to the system.");
            return;
        }
        else if (logger.IsEnabled(LogLevel.Information)) {
            logger.LogInformation("There's {deviceCount} devices connected", devices.Count);
            foreach (UsbmuxdDevice device in devices) {
                logger.LogInformation("Device found: {DeviceId} - {Serial}", device.DeviceId, device.Serial);
            }
        }

        using (LockdownClient lockdown = await MobileDevice.CreateUsingUsbmuxAsync(logger: logger)) {
            await using (AfcService afc = new AfcService(lockdown, logger: logger)) {
                await afc.StartAsync();

                Dictionary<string, string> deviceInfo = await afc.GetDeviceInfoAsync(tokenSource.Token);
                if (logger.IsEnabled(LogLevel.Information)) {
                    logger.LogInformation("Attached AFC device info:");
                    foreach (KeyValuePair<string, string> info in deviceInfo) {
                        logger.LogInformation("{Key} - {Value}", info.Key, info.Value);
                        await Task.Delay(500, tokenSource.Token);
                    }
                }
            }

            // Listen to the heartbeat for ~5 seconds
            await using (HeartbeatService hb = new HeartbeatService(lockdown, logger)) {
                await hb.ConnectAsync(tokenSource.Token);
                try {
                    await hb.StartAsync(TimeSpan.FromSeconds(5), tokenSource.Token);
                }
                catch (Exception ex) when (ex is TimeoutException) {
                    // Nothing to do as this is the expected timeout.
                }
            }

            // Get all installed apps and list them out
            await using (InstallationProxyService ip = new InstallationProxyService(lockdown, logger)) {
                await ip.ConnectAsync(tokenSource.Token);

                DictionaryNode apps = await ip.GetAppsAsync(Netimobiledevice.Services.InstallationProxy.ApplicationType.Any, true, ct: tokenSource.Token);
                foreach (KeyValuePair<string, PropertyNode> app in apps) {
                    if (logger.IsEnabled(LogLevel.Information)) {
                        logger.LogInformation("App bundle found: {appIdentifier}", app.Key);
                    }
                    await Task.Delay(100, tokenSource.Token);
                }
            }

            // Copy and write to screen all of the available provisioning profiles.
            await using (MisagentService ma = new MisagentService(lockdown, logger)) {
                await ma.ConnectAsync(tokenSource.Token);

                IReadOnlyList<DictionaryNode> profiles = await ma.CopyAllAsync(tokenSource.Token);
                foreach (DictionaryNode profile in profiles) {
                    if (logger.IsEnabled(LogLevel.Information)) {
                        logger.LogInformation("Profile found: {profile}", PropertyList.SaveAsString(profile, PlistFormat.Xml));
                        await Task.Delay(500, tokenSource.Token);
                    }
                }
            }

            // Get the current orientation of the device.
            await using (SpringBoardServicesService sb = new SpringBoardServicesService(lockdown, logger)) {
                await sb.ConnectAsync(tokenSource.Token);

                ScreenOrientation orientation = await sb.GetInterfaceOrientationAsync(tokenSource.Token);
                if (logger.IsEnabled(LogLevel.Information)) {
                    logger.LogInformation("Device Interface Orientation: {orientation}", orientation);

                }
                await Task.Delay(500, tokenSource.Token);
            }
        }

        // TODO below this line hasn't been finished yet.
        using (UsbmuxLockdownClient lockdown = await MobileDevice.CreateUsingUsbmuxAsync(logger: logger)) {
            await using (Mobilebackup2Service mb2 = new Mobilebackup2Service(lockdown, logger: logger)) {
                await mb2.ConnectAsync(tokenSource.Token);

                mb2.BeforeReceivingFile += BackupJob_BeforeReceivingFile;
                mb2.Completed += BackupJob_Completed;
                mb2.Error += BackupJob_Error;
                mb2.FileReceived += BackupJob_FileReceived;
                mb2.FileReceiving += BackupJob_FileReceiving;
                mb2.FileTransferError += BackupJob_FileTransferError;
                mb2.PasscodeRequiredForBackup += BackupJob_PasscodeRequiredForBackup;
                mb2.Progress += BackupJob_Progress;
                mb2.Status += BackupJob_Status;
                mb2.Started += BackupJob_Started;

                await mb2.Backup(false, false, true, "backups", tokenSource.Token);
            }
        }
    }

    private static void NotificationProxy_ReceivedNotification(object? sender, ReceivedNotificationEventArgs e) {
        Console.WriteLine(e.Event);
    }

    private static void BackupJob_Started(object? sender, EventArgs e) {
        Console.WriteLine($"BackupJob_Started");
    }

    private static void BackupJob_Status(object? sender, StatusEventArgs e) {
        Console.WriteLine($"BackupJob_Status");
    }

    private static void BackupJob_Progress(object? sender, ProgressChangedEventArgs e) {
        Console.WriteLine($"BackupJob_Progress");
    }

    private static void BackupJob_PasscodeRequiredForBackup(object? sender, EventArgs e) {
        Console.WriteLine($"BackupJob_PasscodeRequiredForBackup");
    }

    private static void BackupJob_FileTransferError(object? sender, BackupFileErrorEventArgs e) {
        Console.WriteLine($"BackupJob_FileTransferError");
    }

    private static void BackupJob_FileReceiving(object? sender, BackupFileEventArgs e) {
        Console.WriteLine($"BackupJob_FileReceiving");
    }

    private static void BackupJob_Completed(object? sender, BackupResultEventArgs e) {
        Console.WriteLine($"BackupJob_Completed");
    }

    private static void BackupJob_Error(object? sender, ErrorEventArgs e) {
        Console.WriteLine($"BackupJob_Error");
    }

    private static void BackupJob_FileReceived(object? sender, BackupFileEventArgs e) {
        Console.WriteLine($"BackupJob_FileReceived");
    }

    private static void BackupJob_BeforeReceivingFile(object? sender, BackupFileEventArgs e) {
        Console.WriteLine($"BackupJob_BeforeReceivingFile");
    }

    private static void Progress_ProgressChanged(object? sender, PairingState e) {
        Console.WriteLine($"Pair Progress Changed: {e}");
    }

    private static async Task Remoted(ILogger logger) {
        Tunneld tunneld = new Tunneld(TunnelProtocol.Tcp, logger: logger);
        tunneld.Start();

        await Task.Delay(10 * 1000);

        RemoteServiceDiscoveryService rsd = await tunneld.GetDevice() ?? throw new LockdownException("No device found");
        await using (Mobilebackup2Service mb2 = new Mobilebackup2Service(rsd, logger: logger)) {
            mb2.BeforeReceivingFile += BackupJob_BeforeReceivingFile;
            mb2.Completed += BackupJob_Completed;
            mb2.Error += BackupJob_Error;
            mb2.FileReceived += BackupJob_FileReceived;
            mb2.FileReceiving += BackupJob_FileReceiving;
            mb2.FileTransferError += BackupJob_FileTransferError;
            mb2.PasscodeRequiredForBackup += BackupJob_PasscodeRequiredForBackup;
            mb2.Progress += BackupJob_Progress;
            mb2.Status += BackupJob_Status;
            mb2.Started += BackupJob_Started;

            await mb2.Backup(false, false, true, "backups", tokenSource.Token);
        }

        await Task.Delay(5 * 1000);
        tunneld.Stop();
    }
}

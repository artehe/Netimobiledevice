using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Netimobiledevice;
using Netimobiledevice.Lockdown;
using Netimobiledevice.NotificationProxy;
using Netimobiledevice.Plist;
using Netimobiledevice.Remoted;
using Netimobiledevice.Remoted.Tunnel;
using Netimobiledevice.Services;
using Netimobiledevice.Services.HouseArrest;
using Netimobiledevice.Services.Mobilebackup2;
using Netimobiledevice.Services.Springboard;
using Netimobiledevice.Usbmuxd;

namespace NetimobiledeviceDemo;

public class Program {
    private static ILogger logger = NullLogger.Instance;
    private static readonly CancellationTokenSource tokenSource = new CancellationTokenSource();

    internal static async Task Main() {
        TaskScheduler.UnobservedTaskException += (sender, args) => {
            logger.LogError("UnobservedTaskException error: {exception}", args.Exception);
        };

        Console.CancelKeyPress += (sender, eventArgs) => {
            logger.LogInformation("Cancellation requested...");
            tokenSource.Cancel();

            // Prevent the process from terminating immediately
            eventArgs.Cancel = true;
        };
        logger.LogInformation("Press Ctrl+C to cancel the operation.");

        using (ILoggerFactory factory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug).AddConsole())) {
            logger = factory.CreateLogger("NetimobiledeviceDemo");

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
                await DemoFeatureRunThrough(lockdown, tokenSource.Token);
            }
        }
    }

    private static async Task DemoFeatureRunThrough(LockdownServiceProvider lsp, CancellationToken ct) {
        await using (AfcService afc = new AfcService(lsp, logger: logger)) {
            await afc.StartAsync(ct);

            Dictionary<string, string> deviceInfo = await afc.GetDeviceInfoAsync(ct);
            if (logger.IsEnabled(LogLevel.Information)) {
                logger.LogInformation("Attached AFC device info:");
                foreach (KeyValuePair<string, string> info in deviceInfo) {
                    logger.LogInformation("{Key} - {Value}", info.Key, info.Value);
                    await Task.Delay(500, ct);
                }
            }
        }

        // Listen to the heartbeat for ~5 seconds
        await using (HeartbeatService hb = new HeartbeatService(lsp, logger)) {
            await hb.ConnectAsync(ct);
            try {
                await hb.StartAsync(TimeSpan.FromSeconds(5), ct);
            }
            catch (Exception ex) when (ex is TimeoutException) {
                // Nothing to do as this is the expected timeout.
            }
        }

        try {
            HouseArrestService ha = await HouseArrestService.CreateAsync(lsp, "test.bundle.app", true, ct);
            await using (ha) { }
        }
        catch (HouseArrestException ex) {
            logger.LogWarning("Failed to start HouseArrestService: {reason}", ex);
        }

        // Get all installed apps and list them out
        await using (InstallationProxyService ip = new InstallationProxyService(lsp, logger)) {
            await ip.ConnectAsync(ct);

            DictionaryNode apps = await ip.GetAppsAsync(Netimobiledevice.Services.InstallationProxy.ApplicationType.Any, true, ct: ct);
            foreach (KeyValuePair<string, PropertyNode> app in apps) {
                if (logger.IsEnabled(LogLevel.Information)) {
                    logger.LogInformation("App bundle found: {appIdentifier}", app.Key);
                }
                await Task.Delay(100, ct);
            }
        }

        // Copy and write to screen all of the available provisioning profiles.
        await using (MisagentService ma = new MisagentService(lsp, logger)) {
            await ma.ConnectAsync(ct);

            IReadOnlyList<DictionaryNode> profiles = await ma.CopyAllAsync(ct);
            foreach (DictionaryNode profile in profiles) {
                if (logger.IsEnabled(LogLevel.Information)) {
                    logger.LogInformation("Profile found: {profile}", PropertyList.SaveAsString(profile, PlistFormat.Xml));
                    await Task.Delay(500, ct);
                }
            }
        }

        await using (Mobilebackup2Service mb2 = new Mobilebackup2Service(lsp, logger: logger)) {
            await mb2.ConnectAsync(ct);

            await mb2.BackupAsync(
                fullBackup: false,
                backupDirectory: "Backups",
                progressCallback: BackupProgressCallback,
                filterCallback: BackupFilterCallback,
                password: "123456",
                patchManifest: true,
                cancellationToken: ct
            );
        }

        // Get the current orientation of the device.
        await using (SpringBoardServicesService sb = new SpringBoardServicesService(lsp, logger)) {
            await sb.ConnectAsync(ct);

            ScreenOrientation orientation = await sb.GetInterfaceOrientationAsync(ct);
            if (logger.IsEnabled(LogLevel.Information)) {
                logger.LogInformation("Device Interface Orientation: {orientation}", orientation);
            }
            await Task.Delay(500, ct);
        }

        // Read 100 lines of the syslog then exit.
        await using (SyslogService sl = new SyslogService(lsp, logger)) {
            await sl.ConnectAsync(ct);

            int count = 0;
            await foreach (string line in sl.WatchAsync(ct)) {
                if (logger.IsEnabled(LogLevel.Debug)) {
                    logger.LogDebug("{syslog}", line);
                }

                count++;
                if (count >= 100) {
                    break;
                }
            }
        }
    }

    private static bool BackupFilterCallback(BackupFile backupFile) {
        return true;
    }

    private static void BackupProgressCallback(double progress) {
        if (logger.IsEnabled(LogLevel.Information)) {
            logger.LogInformation("Backup progress: {progress}", progress);
        }
    }

    private static void NotificationProxy_ReceivedNotification(object? sender, ReceivedNotificationEventArgs e) {
        Console.WriteLine(e.Event);
    }

    // TODO
    private static async Task Remoted(ILogger logger) {
        Tunneld tunneld = new Tunneld(TunnelProtocol.Tcp, logger: logger);
        tunneld.Start();

        await Task.Delay(10 * 1000);

        RemoteServiceDiscoveryService rsd = await tunneld.GetDevice() ?? throw new LockdownException("No device found");
        await using (Mobilebackup2Service mb2 = new Mobilebackup2Service(rsd, logger: logger)) {
        }

        await Task.Delay(5 * 1000);
        tunneld.Stop();
    }
}

using Microsoft.Extensions.Logging;
using Netimobiledevice;
using Netimobiledevice.Backup;
using Netimobiledevice.Heartbeat;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Lockdown.Pairing;
using Netimobiledevice.NotificationProxy;
using Netimobiledevice.Usbmuxd;
using System.ComponentModel;

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

        Usbmux.Subscribe(SubscriptionCallback, SubscriptionErrorCallback, logger);

        List<UsbmuxdDevice> devices = Usbmux.GetDeviceList();
        if (devices.Count == 0) {
            logger.LogError("No device is connected to the system.");
            return;
        }

        logger.LogDebug("There's {deviceCount} devices connected", devices.Count);
        foreach (UsbmuxdDevice device in devices) {
            Console.WriteLine($"Device found: {device.DeviceId} - {device.Serial}");
        }

        using (LockdownClient lockdown = await MobileDevice.CreateUsingUsbmux(logger: logger)) {
            Progress<PairingState> progress = new();
            progress.ProgressChanged += Progress_ProgressChanged;
            if (!lockdown.IsPaired) {
                await lockdown.PairAsync(progress);
            }
        }

        using (LockdownClient lockdown = await MobileDevice.CreateUsingUsbmux(logger: logger)) {
            using (HeartbeatService hb = new HeartbeatService(lockdown, logger)) {
                hb.Start();

                using (NotificationProxyService np = new NotificationProxyService(lockdown)) {
                    np.ReceivedNotification += NotificationProxy_ReceivedNotification;
                    await np.ObserveNotificationAsync(ReceivableNotification.ActivationState).ConfigureAwait(false);
                    await np.ObserveNotificationAsync(ReceivableNotification.AddressBookPreferenceChanged).ConfigureAwait(false);
                    await np.ObserveNotificationAsync(ReceivableNotification.AppInstalled).ConfigureAwait(false);
                    await np.ObserveNotificationAsync(ReceivableNotification.AppUninstalled).ConfigureAwait(false);
                    await np.ObserveNotificationAsync(ReceivableNotification.AttemptActivation).ConfigureAwait(false);
                    await np.ObserveNotificationAsync(ReceivableNotification.BrickState).ConfigureAwait(false);
                    await np.ObserveNotificationAsync(ReceivableNotification.DeveloperImageMounted).ConfigureAwait(false);
                    await np.ObserveNotificationAsync(ReceivableNotification.DeviceNameChanged).ConfigureAwait(false);
                    await np.ObserveNotificationAsync(ReceivableNotification.DiskUsageChanged).ConfigureAwait(false);
                    await np.ObserveNotificationAsync(ReceivableNotification.DsDomainChanged).ConfigureAwait(false);
                    await np.ObserveNotificationAsync(ReceivableNotification.HostAttached).ConfigureAwait(false);
                    await np.ObserveNotificationAsync(ReceivableNotification.HostDetached).ConfigureAwait(false);
                    await np.ObserveNotificationAsync(ReceivableNotification.ItdbprepDidEnd).ConfigureAwait(false);
                    await np.ObserveNotificationAsync(ReceivableNotification.LanguageChanged).ConfigureAwait(false);
                    await np.ObserveNotificationAsync(ReceivableNotification.LocalAuthenticationUiDismissed).ConfigureAwait(false);
                    await np.ObserveNotificationAsync(ReceivableNotification.LocalAuthenticationUiPresented).ConfigureAwait(false);
                    await np.ObserveNotificationAsync(ReceivableNotification.PhoneNumberChanged).ConfigureAwait(false);
                    await np.ObserveNotificationAsync(ReceivableNotification.RegistrationFailed).ConfigureAwait(false);
                    await np.ObserveNotificationAsync(ReceivableNotification.RequestPair).ConfigureAwait(false);
                    await np.ObserveNotificationAsync(ReceivableNotification.SyncCancelRequest).ConfigureAwait(false);
                    await np.ObserveNotificationAsync(ReceivableNotification.SyncResumeRequst).ConfigureAwait(false);
                    await np.ObserveNotificationAsync(ReceivableNotification.SyncSuspendRequst).ConfigureAwait(false);
                    await np.ObserveNotificationAsync(ReceivableNotification.TimezoneChanged).ConfigureAwait(false);
                    await np.ObserveNotificationAsync(ReceivableNotification.TrustedHostAttached).ConfigureAwait(false);

                    await Task.Delay(100000).ConfigureAwait(false);
                }
            }
        }

        using (UsbmuxLockdownClient lockdown = await MobileDevice.CreateUsingUsbmux(logger: logger)) {
            using (Mobilebackup2Service mb2 = new Mobilebackup2Service(lockdown, logger: logger)) {
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

        while (true) {
            await Task.Delay(1000);
        }
    }

    private static void NotificationProxy_ReceivedNotification(object? sender, ReceivedNotificationEventArgs e)
    {
        Console.WriteLine(e.Event);
    }

    private static void BackupJob_Started(object? sender, EventArgs e)
    {
        Console.WriteLine($"BackupJob_Started");
    }

    private static void BackupJob_Status(object? sender, StatusEventArgs e)
    {
        Console.WriteLine($"BackupJob_Status");
    }

    private static void BackupJob_Progress(object? sender, ProgressChangedEventArgs e)
    {
        Console.WriteLine($"BackupJob_Progress");
    }

    private static void BackupJob_PasscodeRequiredForBackup(object? sender, EventArgs e)
    {
        Console.WriteLine($"BackupJob_PasscodeRequiredForBackup");
    }

    private static void BackupJob_FileTransferError(object? sender, BackupFileErrorEventArgs e)
    {
        Console.WriteLine($"BackupJob_FileTransferError");
    }

    private static void BackupJob_FileReceiving(object? sender, BackupFileEventArgs e)
    {
        Console.WriteLine($"BackupJob_FileReceiving");
    }

    private static void BackupJob_Completed(object? sender, BackupResultEventArgs e)
    {
        Console.WriteLine($"BackupJob_Completed");
    }

    private static void BackupJob_Error(object? sender, ErrorEventArgs e)
    {
        Console.WriteLine($"BackupJob_Error");
    }

    private static void BackupJob_FileReceived(object? sender, BackupFileEventArgs e)
    {
        Console.WriteLine($"BackupJob_FileReceived");
    }

    private static void BackupJob_BeforeReceivingFile(object? sender, BackupFileEventArgs e)
    {
        Console.WriteLine($"BackupJob_BeforeReceivingFile");
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

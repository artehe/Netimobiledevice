using Microsoft.Extensions.Logging;
using Netimobiledevice.Exceptions;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Plist;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.NotificationProxy
{
    public sealed class NotificationProxyService : LockdownService
    {
        private const string LOCKDOWN_SERVICE_NAME = "com.apple.mobile.notification_proxy";
        private const string RSD_SERVICE_NAME = "com.apple.mobile.notification_proxy.shim.remote";

        private const string INSECURE_LOCKDOWN_SERVICE_NAME = "com.apple.mobile.insecure_notification_proxy";
        private const string RSD_INSECURE_SERVICE_NAME = "com.apple.mobile.insecure_notification_proxy.shim.remote";

        private static readonly SemaphoreSlim serviceLockSemaphoreSlim = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Device-To-Host notifications.
        /// </summary>
        private static readonly Dictionary<ReceivableNotification, string> receivableNotifications = new() {
            { ReceivableNotification.SyncCancelRequest, "com.apple.itunes-client.syncCancelRequest" },
            { ReceivableNotification.SyncSuspendRequst, "com.apple.itunes-client.syncSuspendRequest" },
            { ReceivableNotification.SyncResumeRequst, "com.apple.itunes-client.syncResumeRequest" },
            { ReceivableNotification.PhoneNumberChanged, "com.apple.mobile.lockdown.phone_number_changed" },
            { ReceivableNotification.DeviceNameChanged, "com.apple.mobile.lockdown.device_name_changed" },
            { ReceivableNotification.TimezoneChanged, "com.apple.mobile.lockdown.timezone_changed" },
            { ReceivableNotification.TrustedHostAttached, "com.apple.mobile.lockdown.trusted_host_attached" },
            { ReceivableNotification.HostDetached, "com.apple.mobile.lockdown.host_detached" },
            { ReceivableNotification.HostAttached, "com.apple.mobile.lockdown.host_attached" },
            { ReceivableNotification.RegistrationFailed, "com.apple.mobile.lockdown.registration_failed" },
            { ReceivableNotification.ActivationState, "com.apple.mobile.lockdown.activation_state" },
            { ReceivableNotification.BrickState, "com.apple.mobile.lockdown.brick_state" },
            { ReceivableNotification.DiskUsageChanged, "com.apple.mobile.lockdown.disk_usage_changed" },
            { ReceivableNotification.DsDomainChanged, "com.apple.mobile.data_sync.domain_changed" },
            { ReceivableNotification.AppInstalled, "com.apple.mobile.application_installed" },
            { ReceivableNotification.AppUninstalled, "com.apple.mobile.application_uninstalled" },
            { ReceivableNotification.DeveloperImageMounted, "com.apple.mobile.developer_image_mounted" },
            { ReceivableNotification.AttemptActivation, "com.apple.springboard.attemptactivation" },
            { ReceivableNotification.ItdbprepDidEnd, "com.apple.itdbprep.notification.didEnd" },
            { ReceivableNotification.LanguageChanged, "com.apple.language.changed" },
            { ReceivableNotification.AddressBookPreferenceChanged, "com.apple.AddressBook.PreferenceChanged" },
            { ReceivableNotification.RequestPair, "com.apple.mobile.lockdown.request_pair" },
            { ReceivableNotification.LocalAuthenticationUiPresented , "com.apple.LocalAuthentication.ui.presented" },
            { ReceivableNotification.LocalAuthenticationUiDismissed, "com.apple.LocalAuthentication.ui.dismissed" }
        };
        /// <summary>
        /// Host-To-Device notifications.
        /// </summary>
        private static readonly Dictionary<SendableNotificaton, string> sendableNotifications = new() {
            { SendableNotificaton.SyncWillStart,  "com.apple.itunes-mobdev.syncWillStart" },
            { SendableNotificaton.SyncDidStart, "com.apple.itunes-mobdev.syncDidStart" },
            { SendableNotificaton.SyncDidFinish, "com.apple.itunes-mobdev.syncDidFinish" },
            { SendableNotificaton.SyncLockRequest, "com.apple.itunes-mobdev.syncLockRequest" }
        };

        private readonly BackgroundWorker notificationListener;

        private static string ServiceNameUsed { get; set; } = LOCKDOWN_SERVICE_NAME;

        public event EventHandler<ReceivedNotificationEventArgs>? ReceivedNotification;

        public NotificationProxyService(LockdownServiceProvider lockdown, bool useInsecureService = false, ILogger? logger = null) : base(lockdown, ServiceNameUsed, GetNotificationProxyServiceConnection(lockdown, useInsecureService), logger: logger)
        {
            notificationListener = new BackgroundWorker {
                WorkerSupportsCancellation = true
            };
            notificationListener.DoWork += NotificationListener_DoWork;
        }

        private static ServiceConnection? GetNotificationProxyServiceConnection(LockdownServiceProvider lockdown, bool useInsecureService)
        {
            if (lockdown is LockdownClient) {
                if (useInsecureService) {
                    ServiceNameUsed = INSECURE_LOCKDOWN_SERVICE_NAME;
                }
                else {
                    ServiceNameUsed = LOCKDOWN_SERVICE_NAME;
                }
            }
            else {
                if (useInsecureService) {
                    ServiceNameUsed = RSD_INSECURE_SERVICE_NAME;
                }
                else {
                    ServiceNameUsed = RSD_SERVICE_NAME;
                }
            }
            return lockdown.StartLockdownService(ServiceNameUsed);
        }

        public override void Dispose()
        {
            if (notificationListener.IsBusy) {
                notificationListener.CancelAsync();
            }
            notificationListener.Dispose();
            base.Dispose();
        }

        private async Task<string?> GetNotification(CancellationToken cancellationToken = default)
        {
            await serviceLockSemaphoreSlim.WaitAsync(cancellationToken);
            try {
                PropertyNode? plist = await Service.ReceivePlistAsync(cancellationToken);
                if (plist != null) {
                    DictionaryNode dict = plist.AsDictionaryNode();
                    if (dict.ContainsKey("Command") && dict["Command"].AsStringNode().Value == "RelayNotification") {
                        if (dict.ContainsKey("Name")) {
                            string notificationName = dict["Name"].AsStringNode().Value;
                            Logger.LogDebug("Got notification {notificationName}", notificationName);
                            return notificationName;
                        }
                    }
                    else if (dict.ContainsKey("Command") && dict["Command"].AsStringNode().Value == "ProxyDeath") {
                        Logger.LogError("NotificationProxy died");
                        throw new NetimobiledeviceException("Notification proxy died, can't listen to notifications anymore");
                    }
                    else if (dict.ContainsKey("Command")) {
                        Logger.LogWarning("Unknown NotificationProxy command {command}", dict["Command"]);
                    }
                }
            }
            catch (ArgumentException ex) {
                Logger.LogError(ex, "Error");
            }
            finally {
                serviceLockSemaphoreSlim.Release();
            }
            return null;
        }

        private async void NotificationListener_DoWork(object? sender, DoWorkEventArgs e)
        {
            Service.SetTimeout(500);
            do {
                try {
                    string? notification = await GetNotification();
                    if (!string.IsNullOrEmpty(notification)) {
                        KeyValuePair<ReceivableNotification, string> receivableNotificationKeyPair = receivableNotifications.AsEnumerable().First(x => x.Value.Equals(notification, StringComparison.Ordinal));
                        ReceivableNotification receivedNotification = receivableNotificationKeyPair.Key;
                        ReceivedNotification?.Invoke(this, new ReceivedNotificationEventArgs(receivedNotification, notification));
                    }
                }
                catch (IOException) {
                    // If there is an IO exception we also have to assume that the service is closed so we abort the listener
                    break;
                }
                catch (ObjectDisposedException) {
                    // If the object is disposed the most likely reason is that the service is closed
                    break;
                }
                catch (TimeoutException) {
                    Logger.LogDebug("No notifications received yet, trying again");
                }
                catch (Exception ex) {
                    if (!notificationListener.CancellationPending) {
                        Logger.LogError(ex, "Notification proxy listener has an error");
                        throw;
                    }
                }
                await Task.Delay(100);
            } while (!notificationListener.CancellationPending);
        }

        /// <summary>
        /// Inform the iOS device to send a notification on the specified event
        /// </summary>
        /// <param name="name"></param>
        private void RegisterNotification(ReceivableNotification notification)
        {
            string notificationToObserve = receivableNotifications[notification];
            DictionaryNode request = new DictionaryNode() {
                { "Command", new StringNode("ObserveNotification") },
                { "Name", new StringNode(notificationToObserve) }
            };

            serviceLockSemaphoreSlim.Wait();
            try {
                Service.SendPlist(request);
            }
            finally {
                serviceLockSemaphoreSlim.Release();
            }

            if (!notificationListener.IsBusy) {
                notificationListener.RunWorkerAsync();
            }
        }

        /// <summary>
        /// Posts the specified notification.
        /// </summary>
        /// <param name="notification">The notification to post.</param>
        public void Post(SendableNotificaton notification)
        {
            string notificationToSend = sendableNotifications[notification];
            DictionaryNode msg = new DictionaryNode() {
                { "Command", new StringNode("PostNotification") },
                { "Name", new StringNode(notificationToSend) }
            };

            serviceLockSemaphoreSlim.Wait();
            try {
                Service.SendPlist(msg);
            }
            finally {
                serviceLockSemaphoreSlim.Release();
            }
        }

        /// <summary>
        /// Inform the device of the notification we want to observe.
        /// </summary>
        /// <param name="notification"></param>
        public void ObserveNotification(ReceivableNotification notification)
        {
            RegisterNotification(notification);
        }

        /// <summary>
        /// Stops observing any notifications from the device
        /// </summary>
        public void Stop()
        {
            notificationListener.CancelAsync();
        }
    }
}

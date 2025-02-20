using Microsoft.Extensions.Logging;
using Netimobiledevice.Exceptions;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Plist;
using System;
using System.ComponentModel;
using System.IO;
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
                        ReceivedNotification?.Invoke(this, new ReceivedNotificationEventArgs(notification, notification));
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
        /// Posts the specified notification.
        /// </summary>
        /// <param name="notification">The notification to post.</param>
        public void Post(string notification)
        {
            DictionaryNode msg = new DictionaryNode() {
                { "Command", new StringNode("PostNotification") },
                { "Name", new StringNode(notification) }
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
        /// Posts the specified notification.
        /// </summary>
        /// <param name="notification">The notification to post.</param>
        public async Task PostAsync(string notification)
        {
            DictionaryNode msg = new DictionaryNode() {
                { "Command", new StringNode("PostNotification") },
                { "Name", new StringNode(notification) }
            };

            await serviceLockSemaphoreSlim.WaitAsync().ConfigureAwait(false);
            try {
                await Service.SendPlistAsync(msg).ConfigureAwait(false);
            }
            finally {
                serviceLockSemaphoreSlim.Release();
            }
        }

        /// <summary>
        /// Inform the device of the notification we want to observe.
        /// </summary>
        /// <param name="notification"></param>
        public void ObserveNotification(string notification)
        {
            DictionaryNode request = new DictionaryNode() {
                { "Command", new StringNode("ObserveNotification") },
                { "Name", new StringNode(notification) }
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
        /// Inform the device of the notification we want to observe.
        /// </summary>
        /// <param name="notification"></param>
        public async Task ObserveNotificationAsync(string notification)
        {
            DictionaryNode request = new DictionaryNode() {
                { "Command", new StringNode("ObserveNotification") },
                { "Name", new StringNode(notification) }
            };

            await serviceLockSemaphoreSlim.WaitAsync().ConfigureAwait(false);
            try {
                await Service.SendPlistAsync(request).ConfigureAwait(false);
            }
            finally {
                serviceLockSemaphoreSlim.Release();
            }

            if (!notificationListener.IsBusy) {
                notificationListener.RunWorkerAsync();
            }
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

using Microsoft.Extensions.Logging;
using Netimobiledevice.Exceptions;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Plist;
using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.NotificationProxy
{
    /// <summary>
    /// Send and receive notifications from the device for example informing a backup sync is about to occur.
    /// </summary>
    public sealed class NotificationProxyService : LockdownService
    {
        private const string LOCKDOWN_SERVICE_NAME = "com.apple.mobile.notification_proxy";
        private const string RSD_SERVICE_NAME = "com.apple.mobile.notification_proxy.shim.remote";

        private const string INSECURE_LOCKDOWN_SERVICE_NAME = "com.apple.mobile.insecure_notification_proxy";
        private const string RSD_INSECURE_SERVICE_NAME = "com.apple.mobile.insecure_notification_proxy.shim.remote";

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
            return lockdown.StartLockdownService(ServiceNameUsed).GetAwaiter().GetResult();
        }

        public override void Dispose()
        {
            if (notificationListener.IsBusy) {
                notificationListener.CancelAsync();
            }
            notificationListener.Dispose();
            base.Dispose();
        }

        private string? GetNotification()
        {
            try {
                PropertyNode? plist = Service.ReceivePlist();
                if (plist != null) {
                    DictionaryNode dict = plist.AsDictionaryNode();
                    if (dict.TryGetValue("Command", out PropertyNode? commandNode)) {
                        if (commandNode.AsStringNode().Value == "RelayNotification") {
                            if (dict.TryGetValue("Name", out PropertyNode? notificationNameNode)) {
                                string notificationName = notificationNameNode.AsStringNode().Value;
                                Logger.LogDebug("Got notification {notificationName}", notificationName);
                                return notificationName;
                            }
                        }
                        else if (commandNode.AsStringNode().Value == "ProxyDeath") {
                            Logger.LogError("NotificationProxy died");
                            throw new NetimobiledeviceException("Notification proxy died, can't listen to notifications anymore");
                        }
                        else {
                            Logger.LogWarning("Unknown NotificationProxy command {command}", commandNode.AsStringNode().Value);
                        }
                    }
                }
            }
            catch (ArgumentException ex) {
                Logger.LogError(ex, "Error");
            }
            return null;
        }

        private void NotificationListener_DoWork(object? sender, DoWorkEventArgs e)
        {
            Service.SetTimeout(5000);
            do {
                try {
                    string? notification = GetNotification();
                    if (!string.IsNullOrEmpty(notification)) {
                        ReceivedNotification?.Invoke(this, new ReceivedNotificationEventArgs(notification, this.Lockdown.Udid));
                    }
                }
                catch (IOException ex) {
                    Logger.LogDebug(ex, "Recieved IO exception");
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

                Thread.Sleep(100);
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

            Service.SendPlist(msg);
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

            await Service.SendPlistAsync(msg).ConfigureAwait(false);
        }

        /// <summary>
        /// Attempts to observe all builtin receivable notifications.
        /// </summary>
        /// <returns></returns>
        public async Task ObserveAll()
        {
            foreach (PropertyInfo prop in typeof(ReceivableNotification).GetProperties()) {
                string notification = prop.GetValue(typeof(ReceivableNotification), null)?.ToString() ?? string.Empty;
                if (string.IsNullOrEmpty(notification)) {
                    continue;
                }
                await ObserveNotificationAsync(notification).ConfigureAwait(false);
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

            Service.SendPlist(request);
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
            await Service.SendPlistAsync(request).ConfigureAwait(false);
        }

        /// <summary>
        /// Starts observing any notifications from the device if it is not doing so already
        /// </summary>
        public void Start()
        {
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

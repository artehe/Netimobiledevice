using Netimobiledevice.Lockdown;
using Netimobiledevice.Lockdown.Services;
using Netimobiledevice.Plist;
using System.Collections.Generic;

namespace Netimobiledevice.NotificationProxy
{
    /// <summary>
    /// Host-To-Device notifications.
    /// </summary>
    public enum Notification
    {
        /// <summary>
        /// The host notifies the device that it's about to start the backup.
        /// </summary>
        SyncWillStart = 0,
        /// <summary>
        /// The host notifies the device that the backup has started.
        /// </summary>
        SyncDidStart,
        /// <summary>
        /// The host notifies the device that the backup has finished.
        /// </summary>
        SyncDidFinish,
        /// <summary>
        /// The host notifies the device about the lock request.
        /// </summary>
        SyncLockRequest
    }

    public sealed class NotificationProxyService : BaseService
    {
        /// <summary>
        /// Host-To-Device notifications.
        /// </summary>
        private static readonly Dictionary<Notification, string> clientNotifications = new Dictionary<Notification, string>() {
            { Notification.SyncWillStart,  "com.apple.itunes-mobdev.syncWillStart" },
            { Notification.SyncDidStart, "com.apple.itunes-mobdev.syncDidStart" },
            { Notification.SyncDidFinish, "com.apple.itunes-mobdev.syncDidFinish" },
            { Notification.SyncLockRequest, "com.apple.itunes-mobdev.syncLockRequest" }
        };

        private const string SERVICE_NAME = "com.apple.mobile.notification_proxy";
        private const string SERVICE_NAME_INSECURE = "com.apple.mobile.insecure_notification_proxy";

        protected override string ServiceName => SERVICE_NAME;

        public NotificationProxyService(LockdownClient client, bool useInsecureService = false) : base(client, GetServiceConnection(client, useInsecureService)) { }

        private static ServiceConnection GetServiceConnection(LockdownClient client, bool useInsecureService)
        {
            ServiceConnection service;
            if (useInsecureService) {
                service = client.StartService(SERVICE_NAME_INSECURE);
            }
            else {
                service = client.StartService(SERVICE_NAME);
            }
            return service;
        }

        /// <summary>
        /// Posts the specified notification.
        /// </summary>
        /// <param name="notification">The notification to post.</param>
        public void Post(Notification notification)
        {
            string notificationToSend = clientNotifications[notification];
            DictionaryNode msg = new DictionaryNode() {
                { "Command", new StringNode("PostNotification") },
                { "Name", new StringNode(notificationToSend) }
            };
            Service.SendPlist(msg);
        }
    }
}

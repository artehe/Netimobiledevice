using Netimobiledevice.Lockdown;
using Netimobiledevice.Lockdown.Services;
using Netimobiledevice.Plist;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Netimobiledevice.NotificationProxy
{
    public sealed class NotificationProxyService : BaseService
    {
        private const string SERVICE_NAME = "com.apple.mobile.notification_proxy";
        private const string SERVICE_NAME_INSECURE = "com.apple.mobile.insecure_notification_proxy";

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
            { ReceivableNotification.RequestPair, "com.apple.mobile.lockdown.request_pair" }
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
        public void Post(SendableNotificaton notification)
        {
            string notificationToSend = sendableNotifications[notification];
            DictionaryNode msg = new DictionaryNode() {
                { "Command", new StringNode("PostNotification") },
                { "Name", new StringNode(notificationToSend) }
            };
            Service.SendPlist(msg);
        }
    }
}

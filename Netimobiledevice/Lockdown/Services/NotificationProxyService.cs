using Netimobiledevice.Plist;

namespace Netimobiledevice.Lockdown.Services
{
    public sealed class NotificationProxyService : BaseService
    {
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
        /// Send notification to the device's notification_proxy.
        /// </summary>
        public void NotifyPost(string name)
        {
            DictionaryNode msg = new DictionaryNode() {
                { "Command", new StringNode("PostNotification") },
                { "Name", new StringNode(name) }
            };
            Service.SendPlist(msg);
        }

        /// <summary>
        /// Tells the device to send a notification on the specified event.
        /// </summary>
        public void NotifyRegisterDispatch(string name)
        {
            DictionaryNode msg = new DictionaryNode() {
                { "Command", new StringNode("ObserveNotification") },
                { "Name", new StringNode(name) }
            };
            Service.SendPlist(msg);
        }
    }
}

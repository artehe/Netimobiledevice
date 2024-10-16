using Microsoft.Extensions.Logging;
using Netimobiledevice.Exceptions;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Remoted.Xpc;
using System;
using System.Threading.Tasks;

namespace Netimobiledevice.Remoted
{
    public class RemoteServiceDiscoveryService : LockdownServiceProvider
    {
        public const ushort RSD_PORT = 58783;

        private XpcDictionaryObject? peerInfo = null;

        public override ILogger Logger => throw new NotImplementedException();

        public override Version OsVersion => throw new NotImplementedException();

        public LockdownClient? Lockdown { get; private set; }

        public RemoteXPCConnection Service { get; private set; }

        public string? Name { get; private set; }

        public RemoteServiceDiscoveryService(string ip, int port, string? name = null) : base()
        {
            Service = new RemoteXPCConnection(ip, port);
            Name = name;
        }

        public void Close()
        {
            Lockdown?.Close();
            Service.Close();
        }

        public async Task Connect()
        {
            await Service.Connect();
            peerInfo = await Service.ReceiveResponse().ConfigureAwait(false);
            /* TODO
            var udid = peerInfo["Properties"]["UniqueDeviceID"];
            var productType = peerInfo["Properties"]["ProductType"];
            try {
                _lockdown = create_using_remote(self.start_lockdown_service('com.apple.mobile.lockdown.remote.trusted'))
            }
            catch (Exception ex) {
                _lockdown = create_using_remote(self.start_lockdown_service('com.apple.mobile.lockdown.remote.untrusted'))
            }
            */
            var allValues = Lockdown.GetValue();
        }

        /// <summary>
        /// Takes a service name and returns the port which that service is running on if the service exists
        /// </summary>
        /// <param name="name">Service to look for</param>
        /// <returns>Port discovered service runs on</returns>
        public ushort GetServicePort(string name)
        {
            bool found = ((XpcDictionaryObject) peerInfo["Services"]).TryGetValue(name, out XpcObject? serviceObject);
            if (found) {
                XpcDictionaryObject service = (XpcDictionaryObject) serviceObject;
                return (ushort) ((XpcInt64) service["Port"]).Data;
            }
            throw new NetimobiledeviceException($"No such service {name}");
        }

        public override ServiceConnection StartLockdownService(string name, bool useEscrowBag = false, bool useTrustedConnection = true)
        {
            throw new NotImplementedException();
        }

        public RemoteXPCConnection StartRemoteService(string name)
        {
            return new RemoteXPCConnection(Service.Address, GetServicePort(name));
        }
    }
}

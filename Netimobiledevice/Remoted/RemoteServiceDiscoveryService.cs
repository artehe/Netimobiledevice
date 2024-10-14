using Microsoft.Extensions.Logging;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Remoted.Xpc;
using System;
using System.Threading.Tasks;

namespace Netimobiledevice.Remoted
{
    public class RemoteServiceDiscoveryService : LockdownServiceProvider
    {
        public const ushort RSD_PORT = 58783;

        private LockdownClient? _lockdown;
        private XpcDictionaryObject? peerInfo = null;

        public override ILogger Logger => throw new NotImplementedException();

        public override Version OsVersion => throw new NotImplementedException();

        public RemoteXPCConnection Service { get; private set; }

        public RemoteServiceDiscoveryService(string ip, ushort port) : base()
        {
            Service = new RemoteXPCConnection(ip, port);
        }

        public void Close()
        {
            _lockdown?.Close();
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
            var allValues = _lockdown.GetValue();
        }

        public override ServiceConnection StartLockdownService(string name, bool useEscrowBag = false, bool useTrustedConnection = true)
        {
            throw new NotImplementedException();
        }
    }
}

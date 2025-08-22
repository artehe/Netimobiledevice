using Microsoft.Extensions.Logging;
using Netimobiledevice.Exceptions;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Lockdown.Pairing;
using Netimobiledevice.Plist;
using Netimobiledevice.Remoted.Xpc;
using System;
using System.Threading.Tasks;

namespace Netimobiledevice.Remoted
{
    public class RemoteServiceDiscoveryService : LockdownServiceProvider
    {
        private const string TRUSTED_SERVICE_NAME = "com.apple.mobile.lockdown.remote.trusted";
        private const string UNTRUSTED_SERVICE_NAME = "com.apple.mobile.lockdown.remote.untrusted";

        public const ushort RSD_PORT = 58783;

        private XpcDictionary? peerInfo = null;

        public override ILogger Logger => throw new NotImplementedException();

        public override Version OsVersion => throw new NotImplementedException();

        public RemoteLockdownClient? Lockdown { get; private set; }

        public RemoteXPCConnection Service { get; private set; }

        public string? Name { get; private set; }

        public string Udid { get; private set; }

        public string ProductType { get; private set; }

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
            Udid = peerInfo["Properties"].AsXpcDictionary()["UniqueDeviceID"].AsXpcString().Data ?? string.Empty;
            ProductType = peerInfo["Properties"].AsXpcDictionary()["ProductType"].AsXpcString().Data ?? string.Empty;

            try {
                Lockdown = MobileDevice.CreateUsingRemote(await StartLockdownService(TRUSTED_SERVICE_NAME).ConfigureAwait(false));
            }
            catch (Exception) {
                Lockdown = MobileDevice.CreateUsingRemote(await StartLockdownService(UNTRUSTED_SERVICE_NAME).ConfigureAwait(false));
            }
            PropertyNode? allValues = Lockdown.GetValue();
        }

        /// <summary>
        /// Takes a service name and returns the port which that service is running on if the service exists
        /// </summary>
        /// <param name="name">Service to look for</param>
        /// <returns>Port discovered service runs on</returns>
        public ushort GetServicePort(string name)
        {
            if (peerInfo == null) {
                throw new NetimobiledeviceException("peerInfo not set");
            }

            if (peerInfo["Services"].AsXpcDictionary().TryGetValue(name, out XpcObject? serviceObject)) {
                XpcDictionary service = serviceObject.AsXpcDictionary();
                string portString = service["Port"].AsXpcString().Data ?? string.Empty;
                return Convert.ToUInt16(portString);
            }
            throw new NetimobiledeviceException($"No such service {name}");
        }

        public override async Task<ServiceConnection> StartLockdownService(string name, bool useEscrowBag = false, bool useTrustedConnection = true)
        {
            ServiceConnection serviceConnection = StartLockdownServiceWithoutCheckin(name);

            DictionaryNode checkin = new DictionaryNode() {
                { "Label", new StringNode("Netimobiledevice") },
                { "ProtocolVersion", new StringNode("2") },
                { "Request", new StringNode("RSDCheckin") }
            };
            if (useEscrowBag) {
                DictionaryNode pairingRecord = PairRecords.GetLocalPairingRecord(PairRecords.GetRemotePairingRecordFilename(Udid), null, Logger) ?? [];
                string encodedPairRecord = Convert.ToBase64String(pairingRecord["remote_unlock_host_key"].AsDataNode().Value);
                checkin.Add("EscrowBag", new StringNode(encodedPairRecord));
            }

            DictionaryNode response = serviceConnection.SendReceivePlist(checkin)?.AsDictionaryNode() ?? [];
            if (response["Request"].AsStringNode().Value != "RSDCheckin") {
                throw new NetimobiledeviceException($"Invalid response for RSDCheckIn: {response}. Expected \"RSDCheckIn\"");
            }

            response = serviceConnection.ReceivePlist()?.AsDictionaryNode() ?? [];
            if (response["Request"].AsStringNode().Value != "StartService") {
                throw new NetimobiledeviceException($"Invalid response for RSDCheckIn: {response}. Expected \"ServiceService\"");
            }

            return serviceConnection;
        }

        public ServiceConnection StartLockdownServiceWithoutCheckin(string name)
        {
            return ServiceConnection.CreateUsingTcp(Service.Address, GetServicePort(name));
        }

        public RemoteXPCConnection StartRemoteService(string name)
        {
            return new RemoteXPCConnection(Service.Address, GetServicePort(name));
        }

        public override PropertyNode? GetValue(string? domain, string? key)
        {
            return Lockdown.GetValue(domain, key);
        }
    }
}

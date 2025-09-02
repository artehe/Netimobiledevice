using System.Collections.Generic;
using System.Threading.Tasks;

namespace Netimobiledevice.Remoted.Tunnel
{
    public class RemotePairingTunnelService : RemotePairingProtocol
    {
        private readonly ushort _port;

        public string Hostname { get; private set; }
        public override string? RemoteIdentifier { get; }

        public RemotePairingTunnelService(string remoteIdentifier, string hostname, ushort port) : base()
        {
            RemoteIdentifier = remoteIdentifier;
            Hostname = hostname;
            _port = port;
        }

        public override Task<Dictionary<string, object>> ReceiveResponse()
        {
            throw new System.NotImplementedException();
        }

        public override Task SendRequest(Dictionary<string, object> data)
        {
            throw new System.NotImplementedException();
        }

        public override void Close()
        {
            throw new System.NotImplementedException();
        }

        public override Task<TunnelResult> StartTunnel()
        {
            throw new System.NotImplementedException();
        }
    }
}

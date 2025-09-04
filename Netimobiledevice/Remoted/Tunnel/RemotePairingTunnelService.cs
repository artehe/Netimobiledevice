using System.Collections.Generic;
using System.Threading.Tasks;

namespace Netimobiledevice.Remoted.Tunnel;

public class RemotePairingTunnelService : RemotePairingProtocol
{
    private ushort _port;

    public string Hostname { get; private set; }
    public override string RemoteIdentifier { get; }

    public RemotePairingTunnelService(string remoteIdentifier, string hostname, ushort port) : base()
    {
        RemoteIdentifier = remoteIdentifier;
        Hostname = hostname;
        _port = port;
    }

    public override Task<Dictionary<string, object>> ReceiveResponse()
    {
        // TODO
        throw new System.NotImplementedException();
    }

    public override Task SendRequest(Dictionary<string, object> data)
    {
        // TODO
        throw new System.NotImplementedException();
    }

    public override void Close()
    {
        // TODO
        throw new System.NotImplementedException();
    }

    public async Task CloseAsync()
    {
        // TODO
        throw new System.NotImplementedException();
    }

    public override Task<TunnelResult> StartTunnel()
    {
        // TODO
        throw new System.NotImplementedException();
    }
}

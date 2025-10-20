using System.Collections.Generic;
using System.Threading.Tasks;

namespace Netimobiledevice.Remoted.Tunnel;

public class CoreDeviceTunnelService : RemotePairingProtocol {
    private const string SERVICE_NAME = "com.apple.internal.dt.coredevice.untrusted.tunnelservice";

    private readonly RemoteService _remoteService;
    private int? version;

    public CoreDeviceTunnelService(RemoteServiceDiscoveryService rsd) : base() {
        _remoteService = new RemoteService(rsd, SERVICE_NAME);
    }

    public override void Close() {
        // TODO
        throw new System.NotImplementedException();
    }

    public override Task CloseAsync() {
        // TODO
        throw new System.NotImplementedException();
    }

    public override Task<Dictionary<string, object>> ReceiveResponse() {
        // TODO
        throw new System.NotImplementedException();
    }

    public override Task SendRequest(Dictionary<string, object> data) {
        // TODO
        throw new System.NotImplementedException();
    }

    public override Task<TunnelResult> StartTunnel() {
        // TODO
        throw new System.NotImplementedException();
    }
}

using Netimobiledevice.Remoted.Xpc;
using System.Threading;
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
        _remoteService.Close();
    }

    public override async Task<XpcDictionary> ReceiveResponseAsync() {
        if (_remoteService.Service == null) {
            throw new NetimobiledeviceException("Service is null");
        }

        XpcDictionary response = await _remoteService.Service.ReceiveResponse();
        return response["value"].AsXpcDictionary();
    }

    public override async Task SendRequestAsync(XpcDictionary data) {
        if (_remoteService.Service == null) {
            throw new NetimobiledeviceException("Service is null");
        }

        XpcDictionary request = new XpcDictionary() {
            { "mangledTypeName", new XpcString("RemotePairing.ControlChannelMessageEnvelope") },
            { "value", data }
        };

        await _remoteService.Service.SendRequestAsync(request, CancellationToken.None).ConfigureAwait(false);
    }
}

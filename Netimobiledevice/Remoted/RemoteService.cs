using Netimobiledevice.Remoted.Xpc;
using System.Threading.Tasks;

namespace Netimobiledevice.Remoted;

public class RemoteService(RemoteServiceDiscoveryService rsd, string serviceName) {
    private readonly RemoteServiceDiscoveryService _rsd = rsd;
    private readonly string _serviceName = serviceName;
    private RemoteXPCConnection? _service;

    public void Close() {
        _service?.Close();
    }

    public async Task Connect() {
        _service = _rsd.StartRemoteService(_serviceName);
        await _service.ConnectAsync().ConfigureAwait(false);
    }
}

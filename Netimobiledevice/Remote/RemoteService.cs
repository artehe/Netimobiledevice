using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Netimobiledevice.Remote.Xpc;
using Netimobiledevice.Remoted;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Remote;

public class RemoteService(RemoteServiceDiscoveryService rsd, string serviceName, ILogger? logger = null) {
    private readonly RemoteServiceDiscoveryService _rsd = rsd;
    private readonly string _serviceName = serviceName;

    private RemoteXPCConnection? _service;

    /// <summary>
    /// The internal logger
    /// </summary>
    protected ILogger Logger { get; } = logger ?? NullLogger.Instance;
    public RemoteXPCConnection Service {
        get {
            if (_service is null) {
                throw new NotConnectedException("RemoteService is not connected call Connect() first");
            }
            return _service;
        }

        private set => _service = value;
    }

    public void Close() {
        _rsd.Close();
        _service?.Close();
    }

    public async Task Connect(CancellationToken ct) {
        Service = _rsd.StartRemoteService(_serviceName);
        await Service.Connect(ct);
    }
}

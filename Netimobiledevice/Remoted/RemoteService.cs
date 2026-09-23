using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Netimobiledevice.Remoted.Xpc;
using System.Threading.Tasks;

namespace Netimobiledevice.Remoted;

public class RemoteService(RemoteServiceDiscoveryService rsd, string serviceName) {
    private readonly RemoteServiceDiscoveryService _rsd = rsd;
    private readonly string _serviceName = serviceName;

    /// <summary>
    /// The internal logger
    /// </summary>
    protected ILogger Logger { get; } = NullLogger.Instance;
    public RemoteXPCConnection? Service { get; private set; }

    public void Close() {
        _rsd.Close();
        Service?.Close();
    }

    public async Task Connect() {
        Service = _rsd.StartRemoteService(_serviceName);
        await Service.Connect();
    }
}

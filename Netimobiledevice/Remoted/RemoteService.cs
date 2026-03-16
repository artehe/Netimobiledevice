using Netimobiledevice.Remoted.Xpc;
using System.Threading.Tasks;

namespace Netimobiledevice.Remoted;

public class RemoteService(RemoteServiceDiscoveryService rsd, string serviceName)
{
    private readonly RemoteServiceDiscoveryService _rsd = rsd;
    private readonly string _serviceName = serviceName;

    public RemoteXPCConnection? Service { get; private set;  }

    public void Close()
    {
        _rsd.Close();
        Service?.Close();
    }

    public async Task Connect()
    {
        Service = _rsd.StartRemoteService(_serviceName);
        await Service.Connect();
    }
}

using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Zeroconf;

namespace Netimobiledevice.Remoted.Bonjour
{
    public class BonjourListener
    {
        private readonly Task<IReadOnlyList<IZeroconfHost>> _resolverTask;

        public BonjourListener(string[] serviceNames, NetworkInterface adapter)
        {
            _resolverTask = ZeroconfResolver.ResolveAsync(serviceNames, netInterfacesToSendRequestOn: [adapter]);
        }

        public async Task<IZeroconfHost[]> GenerateAnswer()
        {
            IReadOnlyList<IZeroconfHost> hosts = await _resolverTask.ConfigureAwait(false);
            return hosts.ToArray();
        }
    }
}

using System;
using System.Threading.Tasks;

namespace Netimobiledevice.Remoted
{
    internal class RemoteService : IAsyncDisposable
    {
        private string _serviceName;
        private RemoteServiceDiscoveryService _rsd;
        private RemoteXPCConnection? _service;

        public RemoteService(RemoteServiceDiscoveryService rsd, string serviceName)
        {
            _serviceName = serviceName;
            _rsd = rsd;
        }

        public async Task Connect()
        {
            _service = _rsd.StartRemoteService(_serviceName);
            await _service.Connect().ConfigureAwait(false);
        }

        public async Task Close()
        {
            await _service?.Close().ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            await Close().ConfigureAwait(false);
        }
    }
}

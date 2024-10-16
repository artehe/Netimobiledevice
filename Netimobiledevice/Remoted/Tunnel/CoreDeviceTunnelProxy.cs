using Netimobiledevice.Lockdown;
using System.Threading.Tasks;

namespace Netimobiledevice.Remoted.Tunnel
{
    public class CoreDeviceTunnelProxy(LockdownServiceProvider lockdown) : StartTcpTunnel
    {
        private const string SERVICE_NAME = "com.apple.internal.devicecompute.CoreDeviceProxy";

        private readonly LockdownServiceProvider _lockdown = lockdown;

        private ServiceConnection? _service;

        public override string RemoteIdentifier => _lockdown.Udid;

        public override void Close()
        {
            _service?.Close();
        }

        /* TODO
    @asynccontextmanager
    async def start_tcp_tunnel(self) -> AsyncGenerator['TunnelResult', None]:
        self._service = await self._lockdown.aio_start_lockdown_service(self.SERVICE_NAME)
        tunnel = RemotePairingTcpTunnel(self._service.reader, self._service.writer)
        handshake_response = await tunnel.request_tunnel_establish()
        tunnel.start_tunnel(handshake_response['clientParameters']['address'],
                            handshake_response['clientParameters']['mtu'])
        try:
            yield TunnelResult(
                tunnel.tun.name, handshake_response['serverAddress'], handshake_response['serverRSDPort'],
                TunnelProtocol.TCP, tunnel)
        finally:
            await tunnel.stop_tunnel()
        */

        public override async Task<TunnelResult> StartTunnel()
        {
            _service = _lockdown.StartLockdownService(SERVICE_NAME);
            RemotePairingTcpTunnel tunnel = new RemotePairingTcpTunnel(_service.Stream);
            dynamic handshakeResponse = tunnel.RequestTunnelEstablish();
            tunnel.StartTunnel(handshakeResponse["clientParameters"]["address"], handshakeResponse["clientParameters"]["mtu"]);
            return new TunnelResult(tunnel.Tun.DeviceIdentification, handshakeResponse["serverAddress"], handshakeResponse["serverRSDPort"], TunnelProtocol.TCP, tunnel);
        }
    }
}

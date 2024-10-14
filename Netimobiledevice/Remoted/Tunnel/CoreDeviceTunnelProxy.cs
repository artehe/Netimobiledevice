namespace Netimobiledevice.Remoted.Tunnel
{
    public class CoreDeviceTunnelProxy : StartTcpTunnel
    {
        /* TODO
    SERVICE_NAME = 'com.apple.internal.devicecompute.CoreDeviceProxy'

    def __init__(self, lockdown: LockdownServiceProvider) -> None:
        self._lockdown = lockdown
        self._service: Optional[ServiceConnection] = None

    @property
    def remote_identifier(self) -> str:
        return self._lockdown.udid

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

    async def close(self) -> None:
        if self._service is not None:
            await self._service.aio_close()
        */
    }
}

namespace Netimobiledevice.Remoted.Tunnel
{
    public abstract class StartTcpTunnel
    {
        public abstract string RemoteIdentifier { get; }
    }

    /* TODO
class StartTcpTunnel(ABC):
    REQUESTED_MTU = 16000

    @abstractmethod
    async def start_tcp_tunnel(self) -> AsyncGenerator[TunnelResult, None]:
        pass
    */
}

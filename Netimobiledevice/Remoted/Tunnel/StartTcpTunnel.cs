using System.Threading.Tasks;

namespace Netimobiledevice.Remoted.Tunnel
{
    public abstract class StartTcpTunnel
    {
        public const int REQUESTED_MTU = 16000;

        public abstract string RemoteIdentifier { get; }

        public abstract void Close();

        public abstract Task<TunnelResult> StartTunnel();
    }
}

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

using Netimobiledevice.Lockdown;
using System;
using System.Threading.Tasks;

namespace Netimobiledevice.Remoted.Tunnel;

public class CoreDeviceTunnelProxy(LockdownServiceProvider lockdown) : StartTcpTunnel {
    private const string SERVICE_NAME = "com.apple.internal.devicecompute.CoreDeviceProxy";

    private readonly LockdownServiceProvider _lockdown = lockdown;

    private ServiceConnection? _service;

    public override string RemoteIdentifier => _lockdown.Udid;

    public override void Close() {
        _service?.Close();
    }

    public override async Task<TunnelResult> StartTcpTunnelAsync() {
        _service = await _lockdown.StartLockdownServiceAsync(SERVICE_NAME).ConfigureAwait(false);
        RemotePairingTcpTunnel tunnel = new RemotePairingTcpTunnel(_service.Stream);
        EstablishTunnelResponse handshakeResponse = tunnel.RequestTunnelEstablish();
        tunnel.StartTunnel(handshakeResponse.ClientParameters.Address, handshakeResponse.ClientParameters.Mtu);

        string tunnelName = string.Empty;
        if (OperatingSystem.IsWindows()) {
            tunnelName = tunnel.Tun?.Name ?? string.Empty;
        }
        return new TunnelResult(tunnelName, handshakeResponse.ServerAddress, handshakeResponse.ServerRSDPort, TunnelProtocol.Tcp, tunnel);
    }
}

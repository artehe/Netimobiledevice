using Netimobiledevice.Lockdown;
using System;
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
            // Validate service
            _service = await _lockdown.StartLockdownServiceAsync(SERVICE_NAME).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Failed to start lockdown service.");

            // Initialize tunnel
            var tunnel = new RemotePairingTcpTunnel(_service.Stream);

            // Perform handshake and validate
            EstablishTunnelResponse? handshakeResponse = tunnel.RequestTunnelEstablish();
            if (handshakeResponse == null) {
                throw new InvalidOperationException("Handshake failed: no response received from the device.");
            }

            if (handshakeResponse.ClientParameters == null) {
                throw new InvalidOperationException("Handshake response is missing ClientParameters.");
            }

            if (string.IsNullOrWhiteSpace(handshakeResponse.ClientParameters.Address)) {
                throw new InvalidOperationException("Handshake response is missing client address.");
            }

            if (handshakeResponse.ClientParameters.Mtu <= 0) {
                throw new InvalidOperationException("Handshake response contains invalid MTU.");
            }

            if (string.IsNullOrWhiteSpace(handshakeResponse.ServerAddress)) {
                throw new InvalidOperationException("Handshake response is missing server address.");
            }

            if (handshakeResponse.ServerRSDPort <= 0) {
                throw new InvalidOperationException("Handshake response contains invalid server port.");
            }

            // Start tunnel
            tunnel.StartTunnel(handshakeResponse.ClientParameters.Address, handshakeResponse.ClientParameters.Mtu);

            // Build result safely
            string tunName = tunnel?.Tun?.Name ?? "unknown";
            return new TunnelResult(tunName, handshakeResponse.ServerAddress, handshakeResponse.ServerRSDPort, TunnelProtocol.TCP, tunnel);
        }

    }
}

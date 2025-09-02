namespace Netimobiledevice.Remoted.Tunnel;

public record TunnelResult(string Interface, string Address, int Port, TunnelProtocol Protocol, RemotePairingTunnel Client);

namespace Netimobiledevice.Remoted.Tunnel;

public record CoreDeviceTunnelEstablishRequest(
    string Type,
    int Mtu
);

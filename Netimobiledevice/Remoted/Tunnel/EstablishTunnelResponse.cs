namespace Netimobiledevice.Remoted.Tunnel;

public record ClientParameters(
    string Address,
    string Netmask,
    uint Mtu
);

public record EstablishTunnelResponse(
    ClientParameters ClientParameters,
    int ServerRSDPort,
    string Type,
    string ServerAddress
);

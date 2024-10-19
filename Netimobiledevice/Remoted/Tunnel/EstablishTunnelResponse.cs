namespace Netimobiledevice.Remoted.Tunnel
{
    public record EstablishTunnelResponse(
        ClientParameters ClientParameters,
        int ServerRSDPort,
        string Type,
        string ServerAddress
    );

    public record ClientParameters(
        string Address,
        string Netmask,
        uint Mtu
    );
}

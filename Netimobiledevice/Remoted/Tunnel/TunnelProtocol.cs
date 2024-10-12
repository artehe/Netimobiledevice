using System.Runtime.Serialization;

namespace Netimobiledevice.Remoted.Tunnel
{
    public enum TunnelProtocol
    {
        [EnumMember(Value = "tcp")]
        TCP,
        [EnumMember(Value = "quic")]
        QUIC
    }
}
using System.Runtime.Serialization;

namespace Netimobiledevice.Remoted.Tunnel;

public enum TunnelProtocol {
    [EnumMember(Value = "tcp")]
    Tcp,
    [EnumMember(Value = "quic")]
    Quic
}

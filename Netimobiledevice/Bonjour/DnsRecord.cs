using System.Collections.Generic;

namespace Netimobiledevice.Bonjour;

public sealed record DnsRecord {
    public required string Name { get; init; }
    public required ushort Type { get; init; }
    public required ushort Class { get; init; }
    public required uint Ttl { get; init; }

    public string? PtrdName { get; init; }
    public ushort? Priority { get; init; }
    public ushort? Weight { get; init; }
    public ushort? Port { get; init; }
    public string? Target { get; init; }
    public Dictionary<string, string>? Txt { get; init; }
    public string? Address { get; init; }
    public byte[]? Raw { get; init; }
}

using System.Collections.Generic;

namespace Netimobiledevice.Bonjour;

public sealed class ServiceInstance {
    public required string Instance { get; init; }
    /// <summary>
    /// "host.local" (without trailing dot), or None if unresolved
    /// </summary>
    public string? Host { get; init; }
    /// <summary>
    /// SRV port
    /// </summary>
    public required ushort Port { get; init; }
    /// <summary>
    /// IPs with interface names
    /// </summary>
    public List<Address> Addresses { get; init; } = [];
    /// <summary>
    /// TXT key/values
    /// </summary>
    public Dictionary<string, string> Properties { get; init; } = [];
}

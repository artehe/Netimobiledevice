using System.Collections.Generic;

namespace Netimobiledevice.Remoted.Bonjour;

public class ServiceInstance(string instance) {
    public string Instance { get; set; } = instance;
    /// <summary>
    /// "host.local" (without trailing dot), or None if unresolved
    /// </summary>
    public string Host { get; set; } = string.Empty;
    /// <summary>
    /// SRV port
    /// </summary>
    public ushort Port { get; set; }
    /// <summary>
    /// IPs with interface names
    /// </summary>
    public List<Address> Addresses { get; set; } = [];
    /// <summary>
    /// TXT key/values
    /// </summary>
    public Dictionary<string, string> Properties { get; set; } = [];
}

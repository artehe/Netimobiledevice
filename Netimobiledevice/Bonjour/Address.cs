using System;

namespace Netimobiledevice.Bonjour;

public sealed class Address(string ip, string networkInterface) {
    public string Ip { get; set; } = ip;
    /// <summary>
    /// Local interface name (e.g., "en0"), or None if unknown
    /// </summary>
    public string Interface { get; set; } = networkInterface;

    public string FullIp {
        get {
            if (!string.IsNullOrEmpty(Interface) && Ip.StartsWith("fe80:", StringComparison.OrdinalIgnoreCase)) {
                return $"{Ip}%{Interface}";
            }
            return Ip;
        }
    }
}

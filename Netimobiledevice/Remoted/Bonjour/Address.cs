using System;

namespace Netimobiledevice.Remoted.Bonjour;

public class Address(string ip, string @interface) {
    public string Ip { get; set; } = ip;
    /// <summary>
    /// Local interface name (e.g., "en0"), or None if unknown
    /// </summary>
    public string Interface { get; set; } = @interface;

    public string FullIp {
        get {
            if (Interface != null && Ip.StartsWith("fe80:", StringComparison.OrdinalIgnoreCase)) {
                return $"{Ip}%{Interface}";
            }
            return Ip;
        }
    }
}

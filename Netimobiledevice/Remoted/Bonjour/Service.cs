namespace Netimobiledevice.Remoted.Bonjour;

public class Service(string target, ushort port) {
    public string Target { get; set; } = target;
    public ushort Port { get; set; } = port;
}

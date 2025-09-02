using System.Threading.Tasks;

namespace Netimobiledevice.Remoted.Tunnel
{
    public class TunnelTask
    {
        public Task? Task { get; set; }
        public string? Udid { get; set; }
        public TunnelResult? Tunnel { get; set; }
    }
}

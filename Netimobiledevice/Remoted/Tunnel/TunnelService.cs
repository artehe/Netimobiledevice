using Netimobiledevice.Remoted.Bonjour;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Netimobiledevice.Remoted.Tunnel
{
    public static class TunnelService
    {
        public static async Task<CoreDeviceTunnelService> CreateCoreDeviceTunnelServiceUsingRsd(RemoteServiceDiscoveryService rsd, bool autoPair = true)
        {
            CoreDeviceTunnelService service = new CoreDeviceTunnelService(rsd);
            await service.Connect(autoPair: autoPair).ConfigureAwait(false);
            return service;
        }

        public static async Task<List<RemotePairingTunnelService>> GetRemotePairingTunnelServices(int bonjourTimeout = BonjourService.DEFAULT_BONJOUR_TIMEOUT, string? udid = null)
        {
            /* TODO
        result = []
        for answer in await browse_remotepairing(timeout=bonjour_timeout):
            for ip in answer.ips:
                for identifier in iter_remote_paired_identifiers():
                    if udid is not None and identifier != udid:
                        continue
                    conn = None
                    try:
                        conn = await create_core_device_tunnel_service_using_remotepairing(identifier, ip, answer.port)
                        result.append(conn)
                        break
                    except ConnectionAbortedError:
                        if conn is not None:
                            await conn.close()
                    except OSError:
                        if conn is not None:
                            await conn.close()
                        continue
        return result
             */
        }
    }
}

using Netimobiledevice.Exceptions;
using Netimobiledevice.Remoted.Bonjour;
using System.Collections.Generic;
using System.Threading.Tasks;
using Zeroconf;

namespace Netimobiledevice.Remoted.Tunnel
{
    public static class TunnelService
    {
        public static async Task<CoreDeviceTunnelService> CreateCoreDeviceTunnelServiceUsingRsd(RemoteServiceDiscoveryService rsd, bool autoPair = true)
        {
            CoreDeviceTunnelService service = new CoreDeviceTunnelService(rsd);
            await service.Connect(autoPair).ConfigureAwait(false);
            return service;
        }

        public static async Task<List<RemotePairingTunnelService>> GetRemotePairingTunnelServices(int bonjourTimeout = BonjourService.DEFAULT_BONJOUR_TIMEOUT, string? udid = null)
        {
            List<RemotePairingTunnelService> result = [];
            foreach (IZeroconfHost answer in await BonjourService.BrowseRemotePairing(bonjourTimeout).ConfigureAwait(false)) {
                foreach (string ip in answer.IPAddresses) {
                    /* TODO
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
                    */
                }
            }
            return result;
        }

        public static async Task<TunnelResult> StartTunnel(StartTcpTunnel protocolHandler, string[]? secrets = null,
            int maxIdleTimeout = RemotePairingQuicTunnel.MAX_IDLE_TIMEOUT, TunnelProtocol protocol = TunnelProtocol.QUIC)
        {
            if (protocolHandler is CoreDeviceTunnelService) {
                /* TODO
            async with start_tunnel_over_core_device(
                    protocol_handler, secrets=secrets, max_idle_timeout=max_idle_timeout, protocol=protocol) as service:
                yield service
                */

            }
            else if (protocolHandler is RemotePairingTunnelService) {
                /* TODO
                async with start_tunnel_over_remotepairing(
                        protocol_handler, secrets=secrets, max_idle_timeout=max_idle_timeout, protocol=protocol) as service:
                    yield service
                    */
            }
            else if (protocolHandler is CoreDeviceTunnelProxy cdtp) {
                if (protocol != TunnelProtocol.TCP) {
                    throw new NetimobiledeviceException("CoreDeviceTunnelProxy protocol can only be TCP");
                }
                return await cdtp.StartTunnel();
            }
            throw new NetimobiledeviceException("Bad value for protocol handler");
        }
    }
}

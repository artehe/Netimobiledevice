using Netimobiledevice.Exceptions;
using Netimobiledevice.Lockdown.Pairing;
using Netimobiledevice.Remoted.Bonjour;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Zeroconf;

namespace Netimobiledevice.Remoted.Tunnel;

public static class TunnelService {
    public static async Task<RemotePairingTunnelService> CreateCoreDeviceTunnelServiceUsingRemotePairing(string remoteIdentifier, string hostname, ushort port, bool autoPair = true) {
        var service = new RemotePairingTunnelService(remoteIdentifier, hostname, port);
        await service.ConnectAsync(autoPair).ConfigureAwait(false);
        return service;
    }

    public static async Task<CoreDeviceTunnelService> CreateCoreDeviceTunnelServiceUsingRsd(RemoteServiceDiscoveryService rsd, bool autoPair = true) {
        CoreDeviceTunnelService service = new CoreDeviceTunnelService(rsd);
        await service.ConnectAsync(autoPair).ConfigureAwait(false);
        return service;
    }

    public static async Task<List<RemotePairingTunnelService>> GetRemotePairingTunnelServices(int bonjourTimeout = BonjourService.DEFAULT_BONJOUR_TIMEOUT, string? udid = null) {
        List<RemotePairingTunnelService> result = [];
        foreach (IZeroconfHost answer in await BonjourService.BrowseRemotePairing(bonjourTimeout).ConfigureAwait(false)) {
            foreach (string ip in answer.IPAddresses) {
                foreach (string identifier in PairRecords.IterateRemotePairedIdentifiers()) {
                    if (udid is not null && identifier != udid) {
                        continue;
                    }
                    RemotePairingTunnelService? conn = null;
                    try {
                        conn = await CreateCoreDeviceTunnelServiceUsingRemotePairing(identifier, ip, answer.port);
                        result.Append(conn);
                        break;
                    }
                    catch (Exception) {
                        if (conn != null) {
                            await conn.CloseAsync().ConfigureAwait(false);
                        }
                    }
                }
            }
        }
        return result;
    }

    public static async Task<TunnelResult> StartTunnel(
        StartTcpTunnel protocolHandler,
        string[]? secrets = null,
        int maxIdleTimeout = RemotePairingQuicTunnel.MAX_IDLE_TIMEOUT,
        TunnelProtocol protocol = TunnelProtocol.Quic
    ) {
        if (protocolHandler is CoreDeviceTunnelService) {
            /* TODO
        async with start_tunnel_over_core_device(
                protocol_handler, secrets=secrets, max_idle_timeout=max_idle_timeout, protocol=protocol) as service:
            yield service
            */

        }
        else if (protocolHandler is RemotePairingTunnelService rpts) {
            return await StartTunnelOverRemotePairing(rpts, secrets, maxIdleTimeout, protocol).ConfigureAwait(false);
        }
        else if (protocolHandler is CoreDeviceTunnelProxy cdtp) {
            if (protocol != TunnelProtocol.Tcp) {
                throw new NetimobiledeviceException("CoreDeviceTunnelProxy protocol can only be TCP");
            }
            return await cdtp.StartTunnel().ConfigureAwait(false);
        }
        throw new NetimobiledeviceException("Bad value for protocol handler");
    }

    public static async Task<TunnelResult> StartTunnelOverRemotePairing(
        RemotePairingTunnelService remotePairing,
        string[]? secrets = null,
        int maxIdleTimeout = RemotePairingQuicTunnel.MAX_IDLE_TIMEOUT,
        TunnelProtocol protocol = TunnelProtocol.QUIC
    ) {
        if (protocol == TunnelProtocol.QUIC) {
            return await remotePairing.StartQuicTunnel(secrets_log_file: secrets, max_idle_timeout: maxIdleTimeout).ConfigureAwait(false);
        }
        else if (protocol == TunnelProtocol.TCP) {
            return await remotePairing.StartTcpTunnel().ConfigureAwait(false);
        }
        throw new NotImplementedException($"Not implemented tunnel start for protocol {protocol}");
    }
}

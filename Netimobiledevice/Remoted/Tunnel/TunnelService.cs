using Netimobiledevice.Lockdown.Pairing;
using Netimobiledevice.Remoted.Bonjour;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Netimobiledevice.Remoted.Tunnel;

public static class TunnelService {
    private static async Task<RemotePairingTunnelService> CreateCoreDeviceTunnelServiceUsingRemotePairingAsync(
        string remoteIdentifier,
        string hostname,
        ushort port,
        bool autoPair = true
    ) {
        RemotePairingTunnelService service = new(remoteIdentifier, hostname, port);
        await service.ConnectAsync(autoPair).ConfigureAwait(false);
        return service;
    }

    public static async Task<CoreDeviceTunnelService> CreateCoreDeviceTunnelServiceUsingRsd(RemoteServiceDiscoveryService rsd, bool autoPair = true) {
        CoreDeviceTunnelService service = new CoreDeviceTunnelService(rsd);
        await service.ConnectAsync(autoPair).ConfigureAwait(false);
        return service;
    }

    public static async Task<TunnelResult> StartTunnelOverCoreDevice(
        CoreDeviceTunnelService serviceProvider,
        TextWriter? secrets,
        float maxIdleTimeout,
        TunnelProtocol protocol
    ) {
        using (RemotedProcessStopper stopper = new RemotedProcessStopper()) {
            switch (protocol) {
                case TunnelProtocol.Quic: {
                    /* TODO
                    var tunnel = await serviceProvider.StartQuicTunnelAsync(secrets, maxIdleTimeout).ConfigureAwait(false);
                    return tunnel.Result;
                    */
                    throw new NotImplementedException();
                }

                case TunnelProtocol.Tcp: {
                    /* TODO
                    var tunnel = await serviceProvider.StartTcpTunnelAsync().ConfigureAwait(false);
                    return tunnel.Result;
                    */
                    throw new NotImplementedException();
                }

                default: {
                    throw new NotImplementedException("Unknown TunnelProtocol");
                }
            }
        }
    }

    /// <summary>
    /// Get remote pairing tunnel services.
    /// </summary>
    /// <param name="bonjourTimeout">Timeout for Bonjour browsing.</param>
    /// <param name="udid">Optional device identifier filter.</param>
    public static async Task<List<RemotePairingTunnelService>> GetRemotePairingTunnelServicesAsync(
        int bonjourTimeout = BonjourService.DEFAULT_BONJOUR_TIMEOUT,
        string? udid = null
    ) {
        List<RemotePairingTunnelService> result = [];
        foreach (ServiceInstance answer in await BonjourService.BrowseRemotePairingAsync(bonjourTimeout)) {
            foreach (Address address in answer.Addresses) {
                foreach (string identifier in PairRecords.IterateRemotePairedIdentifiers()) {
                    if (udid != null && identifier != udid) {
                        continue;
                    }

                    RemotePairingTunnelService? conn = null;
                    try {
                        conn = await CreateCoreDeviceTunnelServiceUsingRemotePairingAsync(identifier, address.FullIp, answer.Port).ConfigureAwait(false);
                        result.Add(conn);
                        break;
                    }
                    catch (OperationCanceledException) {
                        if (conn != null) {
                            await conn.CloseAsync().ConfigureAwait(false);
                        }
                    }
                    catch (IOException)
                    {
                        if (conn != null) {
                            await conn.CloseAsync().ConfigureAwait(false);
                        }
                        continue;
                    }
                }
            }
        }

        return result;
    }

    public static async Task<TunnelResult> StartTunnel(
        StartTcpTunnel protocolHandler, 
        TextWriter? secrets = null,
        float maxIdleTimeout = RemotePairingQuicTunnel.MAX_IDLE_TIMEOUT, 
        TunnelProtocol protocol = TunnelProtocol.Quic
    ) {
        if (protocolHandler is CoreDeviceTunnelService cdts) {
            TunnelResult service = await StartTunnelOverCoreDevice(cdts, secrets, maxIdleTimeout, protocol).ConfigureAwait(false);
            return service;
        }
        else if (protocolHandler is RemotePairingTunnelService) {
            /* TODO
            async with start_tunnel_over_remotepairing(
                    protocol_handler, secrets=secrets, max_idle_timeout=max_idle_timeout, protocol=protocol) as service:
                yield service
                */
        }
        else if (protocolHandler is CoreDeviceTunnelProxy cdtp) {
            if (protocol != TunnelProtocol.Tcp) {
                throw new NetimobiledeviceException("CoreDeviceTunnelProxy protocol can only be TCP");
            }
            return await cdtp.StartTunnel();
        }
        throw new NetimobiledeviceException("Bad value for protocol handler");
    }
}

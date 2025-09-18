using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Netimobiledevice.Usbmuxd;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Remoted.Tunnel;

/// <summary>
/// Start the Tunneld service for remote tunneling and monitoring of connections
/// </summary>
/// <param name="protocol">The protocol to use </param>
/// <param name="usbMonitor">Enable usb monitoring</param>
/// <param name="wifiMonitor">Enable wifi monitoring</param>
/// <param name="usbmuxMonitor">Enable usbmux monitoring</param>
/// <param name="mobdev2Monitor">Enable mobdev2 monitoring</param>
/// <remarks>sudo/admin privilleges are required for one or more monitoring tasks to run.</remarks>
public class Tunneld(
    TunnelProtocol protocol = TunnelProtocol.Tcp,
    bool wifiMonitor = true,
    bool usbMonitor = true,
    bool usbmuxMonitor = true,
    bool mobdev2Monitor = true,
    ILogger? logger = null
) {
    private const int REATTEMPT_COUNT = 5;
    private const int REATTEMPT_INTERVAL = 5000;

    private const int MOBDEV2_INTERVAL = 5000;
    private const int REMOTEPAIRING_INTERVAL = 5000;
    private const int USBMUX_INTERVAL = 2000;

    private readonly List<Task> _tasks = [];
    private readonly ConcurrentDictionary<string, TunnelTask> _tunnelTasks = [];
    private readonly TunnelProtocol _protocol = protocol;

    private CancellationTokenSource _cts = new CancellationTokenSource();
    private Task? _monitoringTask;

    // Different monitoring methods for remoted that are available to use
    private readonly bool _mobdev2Monitor = mobdev2Monitor;
    private readonly bool _usbMonitor = usbMonitor;
    private readonly bool _usbmuxMonitor = usbmuxMonitor;
    private readonly bool _wifiMonitor = wifiMonitor;

    /// <summary>
    /// The internal logger
    /// </summary>
    private ILogger Logger { get; } = logger ?? NullLogger.Instance;

    private Dictionary<string, TunnelDefinition> ListTunnels() {
        Dictionary<string, TunnelDefinition> tunnels = [];
        foreach (KeyValuePair<string, TunnelTask> item in _tunnelTasks) {
            TunnelTask task = item.Value;
            if (string.IsNullOrEmpty(task.Udid) || task.Tunnel == null) {
                continue;
            }
            if (!tunnels.ContainsKey(task.Udid)) {
                tunnels.Add(task.Udid, new TunnelDefinition(task.Tunnel.Address, task.Tunnel.Port, item.Key));
            }
        }
        return tunnels;
    }

    private async Task MonitorMobdev2Task(CancellationToken cancellationToken) {
        /* TODO
        try:
            while True:
                async for ip, lockdown in get_mobdev2_lockdowns(only_paired=True):
                    if self.tunnel_exists_for_udid(lockdown.udid):
                        # skip tunnel if already exists for this udid
                        continue
                    task_identifier = f'mobdev2-{lockdown.udid}-{ip}'
                    try:
                        tunnel_service = CoreDeviceTunnelProxy(lockdown)
                    except InvalidServiceError:
                        logger.warning(f'[{task_identifier}] failed to start CoreDeviceTunnelProxy - skipping')
                        lockdown.close()
                        continue
                    self.tunnel_tasks[task_identifier] = TunnelTask(
                        task=asyncio.create_task(self.start_tunnel_task(task_identifier, tunnel_service),
                                                 name=f'start-tunnel-task-{task_identifier}'),
                        udid=lockdown.udid
                    )
                await asyncio.sleep(MOVDEV2_INTERVAL)
        except asyncio.CancelledError:
            pass
        */
    }

    private async Task MonitorTask() {
        _tasks.Add(TunnelMonitorTask(_cts.Token));
        if (_mobdev2Monitor) {
            _tasks.Add(MonitorMobdev2Task(_cts.Token));
        }
        if (_usbMonitor) {
            _tasks.Add(MonitorUsbTask(_cts.Token));
        }
        if (_usbmuxMonitor) {
            _tasks.Add(MonitorUsbmuxTask(_cts.Token));
        }
        if (_wifiMonitor) {
            _tasks.Add(MonitorWifiTask(_cts.Token));
        }

        await Task.WhenAll(_tasks).ConfigureAwait(false);
    }

    private async Task MonitorUsbTask(CancellationToken cancellationToken) {
        List<NetworkInterface> previousIps = [];
        while (!cancellationToken.IsCancellationRequested) {
            List<NetworkInterface> currentIps = Utils.GetIPv6Interfaces();

            IEnumerable<NetworkInterface> added = currentIps.Where(x => !previousIps.Contains(x));
            IEnumerable<NetworkInterface> removed = previousIps.Where(x => !currentIps.Contains(x));

            previousIps = currentIps;

            Logger.LogDebug("Added Interfaces: {added}", added);
            Logger.LogDebug("Removed Interfaces: {removed}", removed);

            foreach (NetworkInterface ip in removed) {
                /* TODO
                if ip in self.tunnel_tasks:
                    self.tunnel_tasks[ip].task.cancel()
                    await self.tunnel_tasks[ip].task
                */
            }

            foreach (NetworkInterface ip in added) {
                /* TODO
                    self.tunnel_tasks[ip] = TunnelTask(
                        task=asyncio.create_task(self.handle_new_potential_usb_cdc_ncm_interface_task(ip),
                                                 name=f'handle-new-potential-usb-cdc-ncm-interface-task-{ip}'))

                 */
            }

            // Wait before re-iterating
            await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task MonitorUsbmuxTask(CancellationToken cancellationToken) {
        Logger.LogInformation("Starting MonitorUsbmuxTask");
        try {
            while (!cancellationToken.IsCancellationRequested) {
                try {
                    List<UsbmuxdDevice> devices = await Usbmux.GetDeviceListAsync("", logger, cancellationToken).ConfigureAwait(false);
                    Logger.LogInformation("Found {deviceCount} devices for for usbmux monitoring task", devices.Count);
                    foreach (UsbmuxdDevice muxDevice in devices) {
                        string taskIdentifier = $"usbmux-{muxDevice.Serial}-{muxDevice.ConnectionType}";
                        if (TunnelExistsForUdid(muxDevice.Serial)) {
                            continue;
                        }
                        CoreDeviceTunnelProxy service;
                        try {
                            service = new CoreDeviceTunnelProxy(await MobileDevice.CreateUsingUsbmuxAsync(muxDevice.Serial).ConfigureAwait(false));
                        }
                        catch (Exception) {
                            continue;
                        }

                        _tunnelTasks.TryAdd(taskIdentifier, new TunnelTask {
                            Udid = muxDevice.Serial,
                            Task = StartTunnelTask(taskIdentifier, service, TunnelProtocol.Tcp)
                        });
                    }
                }
                catch (Exception ex) {
                    Logger.LogWarning(ex, "Failed to connect to usbmux. waiting for it to restart");
                }
                finally {
                    await Task.Delay(USBMUX_INTERVAL, cancellationToken);
                }
            }
        }
        catch (TaskCanceledException) {
            return;
        }
    }

    private async Task MonitorWifiTask(CancellationToken cancellationToken) {
        try {
            while (!cancellationToken.IsCancellationRequested) {
                /* TODO
                        for service in await get_remote_pairing_tunnel_services():
                            if service.hostname in self.tunnel_tasks:
                                # skip tunnel if already exists for this ip
                                await service.close()
                                continue
                            if self.tunnel_exists_for_udid(service.remote_identifier):
                                # skip tunnel if already exists for this udid
                                await service.close()
                                continue
                            self.tunnel_tasks[service.hostname] = TunnelTask(
                                task=asyncio.create_task(self.start_tunnel_task(service.hostname, service),
                                                         name=f'start-tunnel-task-wifi-{service.hostname}'),
                                udid=service.remote_identifier
                            )
                        await asyncio.sleep(REMOTEPAIRING_INTERVAL)
                 */
            }
        }
        catch (TaskCanceledException) {
            return;
        }
    }

    private async Task StartTunnelTask(
        string taskIdentifier,
        StartTcpTunnel protocolHandler,
        TunnelProtocol? protocol = null
    ) {
        TunnelProtocol usedProtocol = protocol ?? this._protocol;
        if (protocolHandler is CoreDeviceTunnelProxy) {
            usedProtocol = TunnelProtocol.Tcp;
        }

        TunnelResult? tun = null;
        try {
            if (TunnelExistsForUdid(protocolHandler.RemoteIdentifier)) {
                // Cancel current tunnel creation
                throw new TaskCanceledException();
            }

            tun = await TunnelService.StartTunnel(protocolHandler, protocol: usedProtocol).ConfigureAwait(false);
            if (!TunnelExistsForUdid(protocolHandler.RemoteIdentifier)) {
                _tunnelTasks.AddOrUpdate(
                    taskIdentifier,
                    new TunnelTask {
                        Udid = protocolHandler.RemoteIdentifier,
                        Tunnel = tun
                    },
                    (k, v) => {
                        v.Tunnel = tun;
                        v.Udid = protocolHandler.RemoteIdentifier;
                        return v;
                    }
                );

                Logger.LogInformation("Created tunnel to {address}:{port}", tun.Address, tun.Port);
            }
            else {
                Logger.LogInformation("Not establishing tunnel since there is already an active one for same udid");
            }
        }
        catch (Exception ex) {
            Logger.LogDebug(ex, "Exception in StartTunnelTask");
            if (_tunnelTasks.TryRemove(taskIdentifier, out TunnelTask? task)) {
                if (task.Tunnel.Client != null) {
                    Logger.LogInformation("Disconnected from tunnel {address}:{port}", task.Tunnel.Address, task.Tunnel.Port);
                    task.Tunnel.Client.StopTunnel();
                }

                if (protocolHandler != null) {
                    try {
                        protocolHandler.Close();
                    }
                    catch (Exception e) {
                        Logger.LogDebug(e, "Exception while trying to close protocol handler");
                    }
                }
            }
        }
    }

    private async Task TunnelMonitorTask(CancellationToken cancellationToken) {
        while (!cancellationToken.IsCancellationRequested) {
            List<KeyValuePair<string, TunnelTask>> toRemove = [];
            foreach (KeyValuePair<string, TunnelTask> entry in _tunnelTasks) {
                if (entry.Value.Tunnel != null && entry.Value.Tunnel.Client != null && entry.Value.Tunnel.Client.IsTunnelClosed) {
                    toRemove.Add(entry);
                }
            }

            foreach (KeyValuePair<string, TunnelTask> item in toRemove) {
                _tunnelTasks.TryRemove(item);
            }

            // Re-run every 15 seconds
            await Task.Delay(15 * 1000, cancellationToken);
        }
    }

    public void Start() {
        if (_monitoringTask == null) {
            _cts = new CancellationTokenSource();
            _monitoringTask = Task.Run(MonitorTask, _cts.Token);
        }
    }

    public void Stop() {
        _cts.Cancel();
        _monitoringTask = null;
    }

    public bool TunnelExistsForUdid(string udid) {
        foreach (TunnelTask task in _tunnelTasks.Values) {
            if (task.Udid == udid && task.Tunnel != null) {
                return true;
            }
        }
        return false;
    }

    public async Task<RemoteServiceDiscoveryService?> GetDevice(string? udid = null) {
        List<RemoteServiceDiscoveryService> rsds = await GetTunneldDevices();
        if (rsds.Count == 0) {
            return null;
        }

        RemoteServiceDiscoveryService result;
        if (string.IsNullOrEmpty(udid)) {
            result = rsds[0];
        }
        else {
            // Get the specified device
            result = rsds.First(x => x.Udid == udid);
        }

        foreach (RemoteServiceDiscoveryService rsd in rsds) {
            if (rsd.Udid != result.Udid) {
                rsd.Close();
            }
        }

        return result;
    }

    public async Task<List<RemoteServiceDiscoveryService>> GetTunneldDevices() {
        List<RemoteServiceDiscoveryService> rsds = [];
        Dictionary<string, TunnelDefinition> tunnels = ListTunnels();
        foreach (KeyValuePair<string, TunnelDefinition> tunnel in tunnels) {
            RemoteServiceDiscoveryService rsd = new RemoteServiceDiscoveryService(tunnel.Value.TunnelAddres, tunnel.Value.TunnelPort, tunnel.Value.InterfaceId);
            try {
                await rsd.Connect();
                rsds.Add(rsd);
            }
            catch (Exception ex) {
                Logger.LogWarning(ex, "Failed to connect to rsd service for {device}", tunnel.Key);
            }
        }
        return rsds;
    }
}

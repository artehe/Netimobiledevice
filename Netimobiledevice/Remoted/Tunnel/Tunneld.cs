using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualBasic;
using Microsoft.VisualStudio.Threading;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Remoted.Bonjour;
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
    /// <summary>
    /// USB monitor will periodically forget what interfaces it has seen
    /// and force a full rescan. The value is number of iterations of the
    /// inner loop (which sleeps one second each) before blowing away the
    /// `previousIps` cache.
    /// </summary>
    private const int USB_MONITOR_RESCAN_INTERVAL = 30;

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

    private async Task HandleNewPotentialUsbCdcNcmInterfaceTask(string ip) {
        RemoteServiceDiscoveryService? rsd = null;
        string tunnelTaskId = $"start-tunnel-task-usb-{ip}";
        try {
            // Establish an untrusted RSD handshake
            rsd = new RemoteServiceDiscoveryService(ip, RemoteServiceDiscoveryService.RSD_PORT);

            using (RemotedProcessStopper stopper = new RemotedProcessStopper()) {
                try {
                    await rsd.ConnectAsync().ConfigureAwait(false);
                }
                catch (Exception) {
                    throw new TaskCanceledException();
                }
            }

            if (_protocol == TunnelProtocol.Quic && rsd.OsVersion < new Version(17, 0)) {
                rsd.Close();
                throw new TaskCanceledException();
            }

            await StartTunnelTask(
                tunnelTaskId,
                await TunnelService.CreateCoreDeviceTunnelServiceUsingRsd(rsd).ConfigureAwait(false)
            ).ConfigureAwait(false);
        }
        catch (Exception ex) {
            Logger.LogDebug(ex, "Got exception when running HandleNewPotentialUsbCdcNcmInterfaceTask");
        }
        finally {
            try {
                rsd?.Close();
            }
            catch {
                // Ignore any errors from attempting to close rsd
            }
            _tunnelTasks.TryRemove(tunnelTaskId, out TunnelTask? _);
        }
    }

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
        Logger.LogInformation("Starting MonitorMobdev2Task");
        try {
            while (!cancellationToken.IsCancellationRequested) {
                await foreach ((string? ip, TcpLockdownClient? lockdown) in LockdownService.GetMobdev2Lockdowns(onlyPaired: true)) {
                    if (TunnelExistsForUdid(lockdown.Udid)) {
                        // Skip tunnel if already exists for this udid
                        continue;
                    }
                    string taskIdentifier = $"mobdev2-{lockdown.Udid}-{ip}";

                    CoreDeviceTunnelProxy tunnelService;
                    try {
                        tunnelService = new CoreDeviceTunnelProxy(lockdown);
                    }
                    catch (Exception ex) {
                        Logger.LogWarning(ex, "{taskIdentifier} failed to start CoreDeviceTunnelProxy so skipping", taskIdentifier);
                        lockdown.Close();
                        continue;
                    }

                    _tunnelTasks.TryAdd(taskIdentifier, new TunnelTask {
                        Udid = lockdown.Udid,
                        Task = StartTunnelTask(taskIdentifier, tunnelService)
                    });
                }
                await Task.Delay(MOBDEV2_INTERVAL, cancellationToken);
            }
        }
        catch (TaskCanceledException) {
            return;
        }
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
        int iteration = 0;
        List<NetworkInterface> previousIps = [];
        while (!cancellationToken.IsCancellationRequested) {
            iteration++;

            List<NetworkInterface> currentIps = Utils.GetIPv6Interfaces();
            IEnumerable<NetworkInterface> added = currentIps.Where(x => !previousIps.Contains(x));
            IEnumerable<NetworkInterface> removed = previousIps.Where(x => !currentIps.Contains(x));

            // Periodically forget what we have seen so that we re-attempt
            // tunnels even if the interface didn't disappear / reappear
            if (iteration >= USB_MONITOR_RESCAN_INTERVAL) {
                previousIps = [];
                iteration = 0;
            }
            else {
                previousIps = currentIps;
            }

            Logger.LogDebug("Added Interfaces: {added}", added);
            Logger.LogDebug("Removed Interfaces: {removed}", removed);

            foreach (NetworkInterface ip in removed) {
                string tunnelTaskId = $"start-tunnel-task-usb-{ip.Name}";
                if (_tunnelTasks.ContainsKey(tunnelTaskId)) {
                    CancellationTokenSource localCancellationTokenSource = new();
                    localCancellationTokenSource.Cancel();

                    _tunnelTasks.TryRemove(tunnelTaskId, out TunnelTask? value);
                    if (value != null) {
                        await value.Task.WithCancellation(localCancellationTokenSource.Token);
                    }
                }
            }

            if (added.Any()) {
                // A new interface was attached
                foreach (ServiceInstance answer in await BonjourService.BrowseRemotedAsync()) {
                    foreach (Address address in answer.Addresses) {
                        if (address.Interface.StartsWith("utun", StringComparison.InvariantCulture)) {
                            // Skip already established tunnels
                            continue;
                        }
                        if (_tunnelTasks.ContainsKey(address.Ip)) {
                            // Skip already established tunnels
                            continue;
                        }
                        await HandleNewPotentialUsbCdcNcmInterfaceTask(address.Ip).ConfigureAwait(false);
                    }
                }
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
                foreach (RemotePairingTunnelService service in await TunnelService.GetRemotePairingTunnelServicesAsync()) {
                    if (_tunnelTasks.ContainsKey(service.Hostname)) {
                        // Skip tunnel if already exists for this ip
                        await service.CloseAsync();
                        continue;
                    }
                    if (TunnelExistsForUdid(service.RemoteIdentifier)) {
                        // Skip tunnel if already exists for this udid
                        await service.CloseAsync();
                        continue;
                    }

                    _tunnelTasks.TryAdd(service.Hostname, new TunnelTask() {
                        Task = StartTunnelTask(service.Hostname, service),
                        Udid = service.RemoteIdentifier,
                    });
                }
                await Task.Delay(REMOTEPAIRING_INTERVAL, cancellationToken);
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

using Netimobiledevice.Usbmuxd;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Remoted.Tunnel
{
    public class Tunneld
    {
        private const int REATTEMPT_COUNT = 5;
        private const int REATTEMPT_INTERVAL = 5000;

        private const int MOBDEV2_INTERVAL = 5000;
        private const int REMOTEPAIRING_INTERVAL = 5000;
        private const int USBMUX_INTERVAL = 2000;

        public const string TUNNELD_DEFAULT_HOST = "127.0.0.1";
        public const ushort TUNNELD_DEFAULT_PORT = 49151;

        private CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly BackgroundWorker bw = new BackgroundWorker {
            WorkerSupportsCancellation = true
        };

        private readonly List<Task> _tasks = [];
        private readonly ConcurrentDictionary<string, TunnelTask> _tunnelTasks = [];
        private readonly TunnelProtocol _protocol;
        private readonly bool _usbmuxMonitor;

        // TODO implement these monitoring methods
        private readonly bool _usbMonitor;
        private readonly bool _wifiMonitor;
        private readonly bool _mobdev2Monitor;

        public Tunneld(TunnelProtocol protocol = TunnelProtocol.QUIC,
            bool wifiMonitor = true,
            bool usbMonitor = true,
            bool usbmuxMonitor = true,
            bool mobdev2Monitor = true)
        {
            _protocol = protocol;
            _mobdev2Monitor = mobdev2Monitor;
            _usbmuxMonitor = usbmuxMonitor;
            _usbMonitor = usbMonitor;
            _wifiMonitor = wifiMonitor;

            bw = new BackgroundWorker {
                WorkerSupportsCancellation = true
            };
            bw.DoWork += BackgroundWorker_DoWork;
        }

        private void BackgroundWorker_DoWork(object? sender, DoWorkEventArgs e)
        {
            _cts = new CancellationTokenSource();

            _tasks.Add(TunnelMonitorTask(_cts.Token));
            if (_usbmuxMonitor) {
                _tasks.Add(MonitorUsbmuxTask(_cts.Token));
            }

            while (!bw.CancellationPending) {
                Task.WaitAll([.. _tasks]);
            }
        }

        public void Start()
        {
            _cts.Cancel();
            bw.RunWorkerAsync();
        }

        public void Stop()
        {
            _cts.Cancel();
            bw.CancelAsync();
        }

        public async Task MonitorUsbmuxTask(CancellationToken cancellationToken)
        {
            Debug.WriteLine("Starting MonitorUsbmuxTask");
            try {
                while (!cancellationToken.IsCancellationRequested) {
                    try {
                        List<UsbmuxdDevice> devices = Usbmux.GetDeviceList();
                        Debug.WriteLine($"Found {devices.Count} devices for for usbmux monitoring task");
                        foreach (UsbmuxdDevice muxDevice in devices) {
                            string taskIdentifier = $"usbmux-{muxDevice.Serial}-{muxDevice.ConnectionType}";
                            if (TunnelExistsForUdid(muxDevice.Serial)) {
                                continue;
                            }
                            CoreDeviceTunnelProxy service;
                            try {
                                service = new CoreDeviceTunnelProxy(await MobileDevice.CreateUsingUsbmux(muxDevice.Serial).ConfigureAwait(false));
                            }
                            catch (Exception) {
                                continue;
                            }

                            _tunnelTasks.TryAdd(taskIdentifier, new TunnelTask {
                                Udid = muxDevice.Serial,
                                Task = StartTunnelTask(taskIdentifier, service, TunnelProtocol.TCP)
                            });
                        }
                    }
                    catch (Exception) {
                        Debug.WriteLine("Failed to connect to usbmux. waiting for it to restart");
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

        public bool TunnelExistsForUdid(string udid)
        {
            foreach (TunnelTask task in _tunnelTasks.Values) {
                if (task.Udid == udid && task.Tunnel != null) {
                    return true;
                }
            }
            return false;
        }

        public async Task StartTunnelTask(string taskIdentifier, StartTcpTunnel protocolHandler, TunnelProtocol? protocol = null)
        {
            protocol ??= this._protocol;
            if (protocolHandler is CoreDeviceTunnelProxy) {
                protocol = TunnelProtocol.TCP;
            }

            TunnelResult? tun = null;
            try {
                if (TunnelExistsForUdid(protocolHandler.RemoteIdentifier)) {
                    // Cancel current tunnel creation
                    throw new TaskCanceledException();
                }

                tun = await TunnelService.StartTunnel(protocolHandler, protocol: (TunnelProtocol) protocol);
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

                    Debug.WriteLine($"Created tunnel --rsd {tun.Address} {tun.Port}");
                }
                else {
                    Debug.WriteLine("Not establishing tunnel since there is already an active one for same udid");
                }
            }
            catch (Exception ex) {
                Debug.WriteLine(ex);
                if (_tunnelTasks.TryRemove(taskIdentifier, out TunnelTask? task)) {
                    if (task.Tunnel.Client != null) {
                        Debug.WriteLine($"Disconnected from tunnel --rsd {task.Tunnel.Address} {task.Tunnel.Port}");
                        task.Tunnel.Client.StopTunnel();
                    }

                    if (protocolHandler != null) {
                        try {
                            protocolHandler.Close();
                        }
                        catch (Exception) { }
                    }
                }
            }
        }

        private Dictionary<string, TunnelDefinition> ListTunnels()
        {
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

        public async Task TunnelMonitorTask(CancellationToken cancellationToken)
        {
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

        public async Task<List<RemoteServiceDiscoveryService>> GetTunneldDevices(string host, ushort port)
        {
            List<RemoteServiceDiscoveryService> rsds = [];
            Dictionary<string, TunnelDefinition> tunnels = ListTunnels();
            foreach (KeyValuePair<string, TunnelDefinition> tunnel in tunnels) {
                RemoteServiceDiscoveryService rsd = new RemoteServiceDiscoveryService(tunnel.Value.TunnelAddres, tunnel.Value.TunnelPort, tunnel.Value.InterfaceId);
                try {
                    await rsd.Connect();
                    rsds.Add(rsd);
                }
                catch (Exception ex) {
                    Debug.WriteLine(ex.ToString());
                }
            }
            return rsds;
        }

        public async Task<RemoteServiceDiscoveryService?> GetDevice(string? udid = null)
        {
            List<RemoteServiceDiscoveryService> rsds = await GetTunneldDevices(TUNNELD_DEFAULT_HOST, TUNNELD_DEFAULT_PORT);
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
    }
}

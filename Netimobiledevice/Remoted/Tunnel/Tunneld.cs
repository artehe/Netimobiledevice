using Netimobiledevice.Exceptions;
using Netimobiledevice.Remoted.Bonjour;
using Netimobiledevice.Usbmuxd;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using Zeroconf;

namespace Netimobiledevice.Remoted.Tunnel
{
    public class Tunneld(
        TunnelProtocol protocol = TunnelProtocol.QUIC,
        bool wifiMonitor = true,
        bool usbMonitor = true,
        bool usbmuxMonitor = true,
        bool mobdev2Monitor = true)
    {
        private const int REATTEMPT_COUNT = 5;
        private const int REATTEMPT_INTERVAL = 5000;

        private const int MOBDEV2_INTERVAL = 5000;
        private const int REMOTEPAIRING_INTERVAL = 5000;
        private const int USBMUX_INTERVAL = 2000;

        public const string TUNNELD_DEFAULT_HOST = "127.0.0.1";
        public const ushort TUNNELD_DEFAULT_PORT = 49151;

        private CancellationTokenSource _cts = new CancellationTokenSource();

        private readonly TunnelProtocol _protocol = protocol;
        private readonly List<Task> _tasks = [];
        private readonly Dictionary<string, TunnelTask> _tunnelTasks = [];
        private readonly bool _usbMonitor = usbMonitor;
        private readonly bool _wifiMonitor = wifiMonitor;
        private readonly bool _usbmuxMonitor = usbmuxMonitor;
        private readonly bool _mobdev2Monitor = mobdev2Monitor;

        public void Start()
        {
            _cts.Cancel();
            _tasks.Clear();

            _cts = new CancellationTokenSource();
            if (_usbmuxMonitor) {
                _tasks.Add(Task.Run(() => MonitorUsbmuxTask(_cts.Token)));
            }


            /* TODO
            if (_usbMonitor) {
                _tasks.Add(MonitorUsbTask(_cts.Token));
            }
            if (_wifiMonitor) {
                _tasks.Add(MonitorWifiTask(_cts.Token));
            }
            if (_mobdev2Monitor) {
                _tasks.Add(MonitorMobdev2Task(_cts.Token));
            }
            */
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
                                service = new CoreDeviceTunnelProxy(MobileDevice.CreateUsingUsbmux(muxDevice.Serial));
                            }
                            catch (Exception) {
                                continue;
                            }

                            _tunnelTasks[taskIdentifier] = new TunnelTask {
                                Udid = muxDevice.Serial,
                                Task = StartTunnelTask(taskIdentifier, service, TunnelProtocol.TCP)
                            };
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

        public async Task HandleNewPotentialUsbCdcNcmInterfaceTask(NetworkInterface ip)
        {
            RemoteServiceDiscoveryService? rsd = null;
            try {
                List<IZeroconfHost> answers = [];
                for (int i = 0; i < REATTEMPT_COUNT; i++) {
                    answers = await BonjourService.Browse(BonjourService.RemotedServiceNames, [ip]);
                    if (answers.Count > 0) {
                        break;
                    }
                    await Task.Delay(REATTEMPT_INTERVAL);
                }

                if (answers.Count == 0) {
                    throw new TaskCanceledException();
                }

                // establish an untrusted RSD handshake
                string peerAddress = answers[0].IPAddress;
                rsd = new RemoteServiceDiscoveryService(peerAddress, RemoteServiceDiscoveryService.RSD_PORT);

                try {
                    // TODO stop_remoted_if_required()
                    await rsd.Connect();
                }
                finally {
                    // TODO resume_remoted_if_required()
                }

                if (_protocol == TunnelProtocol.QUIC && rsd.OsVersion < new Version(17, 0)) {
                    rsd.Close();
                    throw new NetimobiledeviceException("Can't use RSD on this device");
                }

                await StartTunnelTask(ip.Id, await TunnelService.CreateCoreDeviceTunnelServiceUsingRsd(rsd));
            }
            catch (Exception ex) {
                Console.WriteLine($"Error: {ex.Message}");
            }
            finally {
                rsd?.Close();
                // in case the tunnel was removed just now
                _tunnelTasks.Remove(ip.Id);
            }
        }

        public async Task StartTunnelTask(string taskIdentifier, StartTcpTunnel protocolHandler, TunnelProtocol? protocol = null)
        {
            protocol ??= this._protocol;
            if (protocolHandler is CoreDeviceTunnelProxy) {
                protocol = TunnelProtocol.TCP;
            }

            bool bailedOut = false;

            TunnelResult? tun = null;
            try {
                if (TunnelExistsForUdid(protocolHandler.RemoteIdentifier)) {
                    // Cancel current tunnel creation
                    throw new TaskCanceledException();
                }

                tun = await TunnelService.StartTunnel(protocolHandler, protocol: (TunnelProtocol) protocol);
                if (!TunnelExistsForUdid(protocolHandler.RemoteIdentifier)) {
                    _tunnelTasks[taskIdentifier].Tunnel = tun;
                    _tunnelTasks[taskIdentifier].Udid = protocolHandler.RemoteIdentifier;
                    Debug.WriteLine($"Created tunnel --rsd {tun.Address} {tun.Port}");
                    tun.Client.Close();
                }
                else {
                    bailedOut = true;
                    Debug.WriteLine("Not establishing tunnel since there is already an active one for same udid");
                }
            }
            catch (Exception ex) {
                Debug.WriteLine(ex);
            }
            finally {
                if (tun != null && !bailedOut) {
                    Debug.WriteLine($"Disconnected from tunnel --rsd {tun.Address} {tun.Port}");
                    tun.Client.StopTunnel();
                }

                if (protocolHandler != null) {
                    try {
                        protocolHandler.Close();
                    }
                    catch (Exception) { }
                }

                _tunnelTasks.Remove(taskIdentifier);
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

        public async Task<List<RemoteServiceDiscoveryService>> GetTunneldDevices(string host, ushort port)
        {
            List<RemoteServiceDiscoveryService> rsds = new List<RemoteServiceDiscoveryService>();

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

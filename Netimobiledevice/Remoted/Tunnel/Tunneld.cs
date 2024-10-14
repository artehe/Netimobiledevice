using Netimobiledevice.Usbmuxd;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace Netimobiledevice.Remoted.Tunnel
{
    public class Tunneld
    {
        private const int MOBDEV2_INTERVAL = 5000;
        private const int REMOTEPAIRING_INTERVAL = 5000;
        private const int USBMUX_INTERVAL = 2000;

        public const string TUNNELD_DEFAULT_HOST = "127.0.0.1";
        public const ushort TUNNELD_DEFAULT_PORT = 49151;

        private TunnelProtocol _protocol;
        private List<Task> _tasks;
        private Dictionary<string, TunnelTask> _tunnelTasks;
        private bool _usbMonitor;
        private bool _wifiMonitor;
        private bool _usbmuxMonitor;
        private bool _mobdev2Monitor;

        public Tunneld(
            TunnelProtocol protocol = TunnelProtocol.QUIC,
            bool wifiMonitor = true,
            bool usbMonitor = true,
            bool usbmuxMonitor = true,
            bool mobdev2Monitor = true)
        {
            _protocol = protocol;
            _tasks = [];
            _tunnelTasks = [];
            _usbMonitor = usbMonitor;
            _wifiMonitor = wifiMonitor;
            _usbmuxMonitor = usbmuxMonitor;
            _mobdev2Monitor = mobdev2Monitor;
        }

        public void Start()
        {
            _tasks.Clear();
            if (_usbMonitor) {
                _tasks.Add(MonitorUsbTask());
            }
            if (_wifiMonitor) {
                _tasks.Add(MonitorWifiTask());
            }
            if (_usbmuxMonitor) {
                _tasks.Add(MonitorUsbmuxTask());
            }
            if (_mobdev2Monitor) {
                _tasks.Add(MonitorMobdev2Task());
            }
        }

        public async Task MonitorUsbTask()
        {
            var previousIps = new List<string>();
            while (true) {
                List<NetworkInterface> currentIps = Utils.GetIPv6Interfaces();
                var added = new List<string>(currentIps.Except(previousIps));
                var removed = new List<string>(previousIps.Except(currentIps));

                previousIps = currentIps;

                foreach (string ip in removed) {
                    if (_tunnelTasks.TryGetValue(ip, out TunnelTask? value)) {
                        value.Task.Dispose();
                        await value.Task;
                    }
                }

                foreach (string ip in added) {
                    _tunnelTasks[ip] = new TunnelTask() {
                        Task = HandleNewPotentialUsbCdcNcmInterfaceTask(ip)
                    };
                }

                // Wait before re-iterating
                await Task.Delay(1000);
            }
        }

        public async Task MonitorWifiTask()
        {
            try {
                while (true) {
                    var services = await GetRemotePairingTunnelServices();
                    foreach (var service in services) {
                        if (_tunnelTasks.ContainsKey(service.Hostname)) {
                            // skip tunnel if already exists for this ip
                            await service.Close();
                            continue;
                        }

                        if (TunnelExistsForUdid(service.RemoteIdentifier)) {
                            // skip tunnel if already exists for this udid
                            await service.Close();
                            continue;
                        }

                        _tunnelTasks[service.Hostname] = new TunnelTask {
                            Task = StartTunnelTask(service.Hostname, service),
                            Udid = service.RemoteIdentifier
                        };
                    }
                    await Task.Delay(REMOTEPAIRING_INTERVAL);
                }
            }
            catch (TaskCanceledException) {
            }
        }

        public async Task MonitorUsbmuxTask()
        {
            try {
                while (true) {
                    try {
                        List<UsbmuxdDevice> muxDevices = Usbmux.GetDeviceList();
                        foreach (UsbmuxdDevice muxDevice in muxDevices) {
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
                    catch (ConnectionFailedToUsbmuxdException) {
                        Console.WriteLine("Failed to connect to usbmux. waiting for it to restart");
                    }
                    finally {
                        await Task.Delay(USBMUX_INTERVAL);
                    }
                }
            }
            catch (TaskCanceledException) {
                return;
            }
        }

        public async Task MonitorMobdev2Task()
        {
            try {
                while (true) {
                    var lockdowns = GetMobdev2Lockdowns(onlyPaired: true);
                    foreach (var lockdown in lockdowns) {
                        if (TunnelExistsForUdid(lockdown.Udid)) {
                            // skip tunnel if already exists for this udid
                            continue;
                        }

                        var taskIdentifier = $"mobdev2-{lockdown.Udid}-{lockdown.Ip}";
                        CoreDeviceTunnelProxy tunnelService;
                        try {
                            tunnelService = new CoreDeviceTunnelProxy(lockdown);
                        }
                        catch (InvalidServiceError) {
                            Console.WriteLine($"[{taskIdentifier}] Failed to start CoreDeviceTunnelProxy - skipping");
                            continue;
                        }

                        _tunnelTasks[taskIdentifier] = new TunnelTask {
                            Task = Task.Run(() => StartTunnelTask(taskIdentifier, tunnelService)),
                            Udid = lockdown.Udid
                        };
                    }

                    await Task.Delay(MOBDEV2_INTERVAL);
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
    }
}

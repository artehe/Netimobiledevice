using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using UniversalTunTapDriver;

namespace Netimobiledevice.Remoted.Tunnel
{
    public abstract class RemotePairingTunnel
    {
        private CancellationTokenSource cts = new CancellationTokenSource();
        private Task? _tunReadTask;

        private byte[] LoopbackHeader => [0x00, 0x00, 0x86, 0xDD];

        public TunTapDevice? Tun { get; private set; }

        public RemotePairingTunnel()
        {
        }

        private static IPAddress GetSubnetMask(IPAddress address)
        {
            foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces()) {
                foreach (UnicastIPAddressInformation unicastIPAddressInformation in adapter.GetIPProperties().UnicastAddresses) {
                    if (unicastIPAddressInformation.Address.AddressFamily == AddressFamily.InterNetwork) {
                        if (address.Equals(unicastIPAddressInformation.Address)) {
                            return unicastIPAddressInformation.IPv4Mask;
                        }
                    }
                }
            }
            throw new ArgumentException(string.Format("Can't find subnetmask for IP address '{0}'", address));
        }

        private async Task TunReadTask(CancellationToken cancellationToken)
        {
            var readSize = Tun.GetMTU() + LoopbackHeader.Length;
            while (!cancellationToken.IsCancellationRequested) {
                if (!OperatingSystem.IsWindows()) {
                    /* TODO
                    async with aiofiles.open(self.tun.fileno(), 'rb', opener=lambda path, flags: path, buffering=0) as f:
                        while True:
                            packet = await f.read(read_size)
                            assert packet.startswith(LOOPBACK_HEADER)
                            packet = packet[len(LOOPBACK_HEADER):]
                            await self.send_packet_to_device(packet)
                     */
                }
                else {
                    /* TODO
                    while True:
                        packet = await asyncio.get_running_loop().run_in_executor(None, self.tun.read)
                        if packet:
                            await self.send_packet_to_device(packet)
                     */
                }
            }
        }

        public byte[] EncodeCdtunnelPacket(Dictionary<string, object> data)
        {
            return new CDTunnelPacket(JsonSerializer.Serialize(data)).GetBytes();
        }

        public virtual void StartTunnel(string address, int mtu)
        {
            cts = new CancellationTokenSource();

            string dInfo = "tun0";
            if (OperatingSystem.IsWindows()) {
                dInfo = Guid.NewGuid().ToString();
            }
            Tun = new TunTapDevice(dInfo);

            Tun.ConfigTun(IPAddress.Loopback, IPAddress.Parse(address), GetSubnetMask(IPAddress.Loopback));
            Tun.CreateDeviceIOStream(mtu);
            _tunReadTask = TunReadTask(cts.Token);
        }

        public abstract void Close();

        public abstract void SendPacketToDevice(byte[] packet);

        public abstract Dictionary<string, object> RequestTunnelEstablish();

        public void StopTunnel()
        {
            Debug.WriteLine("Stopping tunnel");
            cts.Cancel();
            Tun?.Close();
            Tun = null;
        }
    }
}

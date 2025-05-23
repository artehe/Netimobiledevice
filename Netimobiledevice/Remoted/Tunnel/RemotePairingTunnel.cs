﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Remoted.Tunnel
{
    public abstract class RemotePairingTunnel
    {
        private CancellationTokenSource cts = new CancellationTokenSource();
        private Task? _tunReadTask;

        private byte[] LoopbackHeader => [0x00, 0x00, 0x86, 0xDD];

        public TunTapDevice? Tun { get; private set; }

        public abstract bool IsTunnelClosed { get; }

        public RemotePairingTunnel()
        {
        }

        private async Task TunReadTask(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested) {
                if (OperatingSystem.IsWindows()) {
                    while (!cancellationToken.IsCancellationRequested) {
                        byte[] packet = Tun?.Read() ?? [];
                        if (packet.Length > 0) {
                            await SendPacketToDevice(packet);
                        }
                    }
                }
                else {
                    throw new InvalidOperationException("Not implemented anything other than WIndows yet");
                }
            }
        }

        public byte[] EncodeCdtunnelPacket(Dictionary<string, object> data)
        {
            return new CDTunnelPacket(JsonSerializer.Serialize(data)).GetBytes();
        }

        public virtual void StartTunnel(string address, uint mtu)
        {
            cts = new CancellationTokenSource();

            Tun = new TunTapDevice();
            Tun.Address = address;
            Tun.Mtu = mtu;

            Tun.Up();
            _tunReadTask = Task.Run(() => TunReadTask(cts.Token));
        }

        public abstract void Close();

        public abstract Task SendPacketToDevice(byte[] packet);

        public abstract EstablishTunnelResponse RequestTunnelEstablish();

        public void StopTunnel()
        {
            Debug.WriteLine("Stopping tunnel");
            cts.Cancel();
            Tun?.Close();
            Tun = null;
        }
    }
}

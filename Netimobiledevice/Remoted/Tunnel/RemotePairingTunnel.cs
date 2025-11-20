using Netimobiledevice.Utils;
using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Remoted.Tunnel;

public abstract class RemotePairingTunnel {
    private CancellationTokenSource cts = new();
    private Task? _tunReadTask;

    public TunTapDevice? Tun { get; private set; }

    public abstract bool IsTunnelClosed { get; }

    public RemotePairingTunnel() {
    }

    private async Task TunReadTask() {
        if (OperatingSystem.IsWindows()) {
            byte[] packet = Tun == null ? [] : await Tun.ReadAsync(cts.Token).ConfigureAwait(false);
            await SendPacketToDevice(packet).ConfigureAwait(false);
        }
        else {
            throw new InvalidOperationException("Not implemented anything other than Windows yet");
        }
    }

    public static byte[] EncodeCoreDeviceTunnelPacket(CoreDeviceTunnelEstablishRequest data) {
        string json = JsonSerializer.Serialize(data, JsonSerializerSourceGenerationContext.Default.CoreDeviceTunnelEstablishRequest);
        return new CoreDeviceTunnelPacket(json).GetBytes();
    }

    public virtual void StartTunnel(string address, uint mtu) {
        cts = new CancellationTokenSource();

        Tun = new TunTapDevice();
        Tun.SetAddress(address);
        Tun.Mtu = mtu;

        Tun.Up();
        _tunReadTask = Task.Run(TunReadTask, cts.Token);
    }

    public abstract void Close();

    public abstract Task SendPacketToDevice(byte[] packet);

    public abstract EstablishTunnelResponse RequestTunnelEstablish();

    public void StopTunnel() {
        Debug.WriteLine("Stopping tunnel");
        cts.Cancel();
        Tun?.Close();
        Tun = null;
    }
}

using Netimobiledevice.EndianBitConversion;
using Netimobiledevice.Utils;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Remoted.Tunnel;

public class RemotePairingTcpTunnel(Stream stream) : RemotePairingTunnel() {
    private const int REQUESTED_MTU = 16000;
    private const int IPV6_HEADER_SIZE = 40;

    private CancellationTokenSource _cts = new CancellationTokenSource();
    private readonly Stream _stream = stream;

    private Task? _sockReadTask;

    private static byte[] LoopbackHeader => [0x00, 0x00, 0x86, 0xDD];

    public override bool IsTunnelClosed => !_stream.CanWrite || !_stream.CanRead;

    public override void Close() {
        _stream.Close();
        _cts.Cancel();
        _sockReadTask = null;
    }

    public async Task SockReadTask() {
        try {
            while (true) {
                try {
                    byte[] ipv6Header = new byte[IPV6_HEADER_SIZE];
                    await _stream.ReadExactlyAsync(ipv6Header, _cts.Token);

                    ushort ipv6Length = EndianBitConverter.BigEndian.ToUInt16(ipv6Header, 4);
                    byte[] ipv6Body = new byte[ipv6Length];
                    await _stream.ReadExactlyAsync(ipv6Body, _cts.Token);
                    Tun?.Write([.. LoopbackHeader, .. ipv6Header, .. ipv6Body]);
                }
                catch (Exception ex) {
                    // TODO replace with logging
                    Debug.WriteLine(ex);
                    await Task.Delay(1000, _cts.Token);
                }
            }
        }
        catch (Exception ex) {
            // TODO replace with logging
            Debug.WriteLine(ex);
        }
    }

    public override EstablishTunnelResponse RequestTunnelEstablish() {
        CoreDeviceTunnelEstablishRequest request = new CoreDeviceTunnelEstablishRequest(
            "clientHandshakeRequest",
            REQUESTED_MTU
        );
        _stream.Write(EncodeCoreDeviceTunnelPacket(request));

        byte[] buffer = new byte[REQUESTED_MTU];
        _stream.Read(buffer);
        string jsonString = CoreDeviceTunnelPacket.Parse(buffer).JsonBody;
        return JsonSerializer.Deserialize(jsonString, JsonSerializerSourceGenerationContext.Default.EstablishTunnelResponse) ?? throw new NetimobiledeviceException("Failed to establish tunnel");
    }

    public override async Task SendPacketToDevice(byte[] packet) {
        await _stream.WriteAsync(packet);
    }

    public override void StartTunnel(string address, uint mtu) {
        base.StartTunnel(address, mtu);
        if (_sockReadTask != null) {
            _cts.Cancel();
            _sockReadTask = null;
        }
        _cts = new CancellationTokenSource();
        _sockReadTask = Task.Run(SockReadTask, _cts.Token);
    }
}

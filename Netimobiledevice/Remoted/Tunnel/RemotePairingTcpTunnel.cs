using Netimobiledevice.EndianBitConversion;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Netimobiledevice.Remoted.Tunnel
{
    public class RemotePairingTcpTunnel : RemotePairingTunnel
    {
        private const int REQUESTED_MTU = 16000;
        private const int IPV6_HEADER_SIZE = 40;

        private static byte[] LoopbackHeader => [0x00, 0x00, 0x86, 0xDD];

        public override bool IsTunnelClosed => !_stream.CanWrite || !_stream.CanRead;

        private readonly Stream _stream;
        private Task? _sockReadTask;

        public RemotePairingTcpTunnel(Stream stream) : base()
        {
            _stream = stream;
        }

        public override void Close()
        {
            _stream.Close();
        }

        public async Task SockReadTask()
        {
            try {
                while (true) {
                    try {
                        byte[] ipv6Header = new byte[IPV6_HEADER_SIZE];
                        await _stream.ReadExactlyAsync(ipv6Header);

                        ushort ipv6Length = EndianBitConverter.BigEndian.ToUInt16(ipv6Header, 4);
                        byte[] ipv6Body = new byte[ipv6Length];
                        await _stream.ReadExactlyAsync(ipv6Body);
                        Tun?.Write([.. LoopbackHeader, .. ipv6Header, .. ipv6Body]);
                    }
                    catch (Exception ex) {
                        Debug.WriteLine(ex);
                        await Task.Delay(1000);
                    }
                }
            }
            catch (Exception ex) {
                Debug.WriteLine(ex);
            }
        }

        public override EstablishTunnelResponse? RequestTunnelEstablish()
        {
            Dictionary<string, object> message = new Dictionary<string, object>() {
                { "type", "clientHandshakeRequest" },
                { "mtu", REQUESTED_MTU }
            };
            _stream.Write(EncodeCdtunnelPacket(message));

            byte[] buffer = new byte[REQUESTED_MTU];
            _stream.Read(buffer);
            string jsonString = CDTunnelPacket.Parse(buffer).JsonBody;
            return JsonSerializer.Deserialize<EstablishTunnelResponse>(jsonString, new JsonSerializerOptions {
                PropertyNameCaseInsensitive = true
            });
        }

        public override async Task SendPacketToDevice(byte[] packet)
        {
            await _stream.WriteAsync(packet);
        }

        public override void StartTunnel(string address, uint mtu)
        {
            base.StartTunnel(address, mtu);
            _sockReadTask = Task.Run(() => SockReadTask());
        }
    }
}

using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Netimobiledevice.Remoted.Tunnel
{
    public class RemotePairingTcpTunnel : RemotePairingTunnel
    {
        private const int REQUESTED_MTU = 16000;

        private readonly Stream _stream;
        private Task? _sockReadTask;

        public RemotePairingTcpTunnel(Stream stream) : base()
        {
            _stream = stream;
        }

        public override void Close()
        {
            throw new System.NotImplementedException();
        }

        public async Task SockReadTask()
        {
            /* TODO
        @asyncio_print_traceback
        async def sock_read_task(self) -> None:
            try:
                while True:
                    try:
                        ipv6_header = await self._reader.readexactly(IPV6_HEADER_SIZE)
                        ipv6_length = struct.unpack('>H', ipv6_header[4:6])[0]
                        ipv6_body = await self._reader.readexactly(ipv6_length)
                        self.tun.write(LOOPBACK_HEADER + ipv6_header + ipv6_body)
                    except asyncio.exceptions.IncompleteReadError:
                        await asyncio.sleep(1)
            except OSError as e:
                self._logger.warning(f'got {e.__class__.__name__} in {asyncio.current_task().get_name()}')
                await self.wait_closed()
            */
        }

        public override EstablishTunnelResponse RequestTunnelEstablish()
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

        public override void SendPacketToDevice(byte[] packet)
        {
            throw new System.NotImplementedException();
        }

        public override void StartTunnel(EstablishTunnelResponse handshakeResponse)
        {
            base.StartTunnel(handshakeResponse);
            _sockReadTask = Task.Run(() => SockReadTask());
        }
    }
}

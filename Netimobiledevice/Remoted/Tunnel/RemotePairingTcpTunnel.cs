using System.Collections.Generic;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;

namespace Netimobiledevice.Remoted.Tunnel
{
    public class RemotePairingTcpTunnel : RemotePairingTunnel
    {
        private const int REQUESTED_MTU = 16000;

        private readonly NetworkStream _stream;

        public RemotePairingTcpTunnel(NetworkStream networkStream) : base()
        {
            _stream = networkStream;
        }

        public async Task<dynamic> RequestTunnelEstablish()
        {
            Dictionary<string, object> message = new Dictionary<string, object>() {
                { "type", "clientHandshakeRequest" },
                { "mtu", REQUESTED_MTU }
            };
            await _stream.WriteAsync(EncodeCdtunnelPacket(message)).ConfigureAwait(false);

            byte[] buffer = new byte[REQUESTED_MTU];
            await _stream.ReadAsync(buffer);
            string jsonString = CDTunnelPacket.Parse(buffer).JsonBody;
            return JsonSerializer.Deserialize<dynamic>(jsonString);
        }

        public override void StartTunnel(string address, int mtu)
        {
            base.StartTunnel(address, mtu);
            /* TODO self._sock_read_task = asyncio.create_task(self.sock_read_task(), name=f'sock-read-task-{address}') */
        }

    }

    /* TODO
    def __init__(self, reader: StreamReader, writer: StreamWriter):
        RemotePairingTunnel.__init__(self)
        self._reader = reader
        self._writer = writer
        self._sock_read_task = None

    async def send_packet_to_device(self, packet: bytes) -> None:
        self._writer.write(packet)
        await self._writer.drain()

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

    async def wait_closed(self) -> None:
        try:
            await self._writer.wait_closed()
        except OSError:
            pass

    async def stop_tunnel(self) -> None:
        self._sock_read_task.cancel()
        with suppress(CancelledError):
            await self._sock_read_task
        if not self._writer.is_closing():
            self._writer.close()
            try:
                await self._writer.wait_closed()
            except OSError:
                pass
        await super().stop_tunnel()
    */

}

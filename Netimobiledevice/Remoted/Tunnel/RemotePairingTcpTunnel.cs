using System.Collections.Generic;
using System.Net.Sockets;
using System.Text.Json;

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

        public override void Close()
        {
            throw new System.NotImplementedException();
        }

        public override Dictionary<string, object> RequestTunnelEstablish()
        {
            Dictionary<string, object> message = new Dictionary<string, object>() {
                { "type", "clientHandshakeRequest" },
                { "mtu", REQUESTED_MTU }
            };
            _stream.Write(EncodeCdtunnelPacket(message));

            byte[] buffer = new byte[REQUESTED_MTU];
            _stream.Read(buffer);
            string jsonString = CDTunnelPacket.Parse(buffer).JsonBody;
            return JsonSerializer.Deserialize<dynamic>(jsonString);
        }

        public override void SendPacketToDevice(byte[] packet)
        {
            throw new System.NotImplementedException();
        }

        public override void StartTunnel(string address, int mtu)
        {
            base.StartTunnel(address, mtu);
            /* TODO self._sock_read_task = asyncio.create_task(self.sock_read_task(), name=f'sock-read-task-{address}') */
        }
    }
}

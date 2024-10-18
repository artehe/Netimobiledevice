namespace Netimobiledevice.Remoted.Tunnel
{
    public class RemotePairingQuicTunnel : RemotePairingTunnel
    {
        public const int MAX_IDLE_TIMEOUT = 30 * 1000;

        public override void Close()
        {
            throw new System.NotImplementedException();
        }

        public override EstablishTunnelResponse RequestTunnelEstablish()
        {
            throw new System.NotImplementedException();
        }

        public override void SendPacketToDevice(byte[] packet)
        {
            throw new System.NotImplementedException();
        }
    }
}

using Netimobiledevice.Remoted.Tunnel;

namespace Netimobiledevice.Remoted
{
    public static class Remote
    {
        /// <summary>
        /// Start the Tunneld service for remote tunneling
        /// </summary>
        /// <param name="host"></param>
        /// <param name="port"></param>
        /// <param name="deamonise"></param>
        /// <param name="protocol"></param>
        /// <param name="usb">Enable usb monitoring</param>
        /// <param name="wifi">Enable wifi monitoring</param>
        /// <param name="usbmux">Enable usbmux monitoring</param>
        /// <param name="mobdev2">Enable mobdev2 monitoring</param>
        public static Tunneld StartTunneld(string host = Tunneld.TUNNELD_DEFAULT_HOST, ushort port = Tunneld.TUNNELD_DEFAULT_PORT,
            TunnelProtocol protocol = TunnelProtocol.QUIC, bool usb = true, bool wifi = true, bool usbmux = true, bool mobdev2 = true)
        {
            // TODO @sudo_required
            Tunneld tunneld = new Tunneld(protocol, wifi, usb, usbmux, mobdev2);
            tunneld.Start();
            return tunneld;
        }
    }
}

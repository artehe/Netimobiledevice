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
        public static void StartTunneld(string host = Tunneld.TUNNELD_DEFAULT_HOST, ushort port = Tunneld.TUNNELD_DEFAULT_PORT
            bool deamonise = true, TunnelProtocol protocol = TunnelProtocol.TCP, bool usb = true, bool wifi = true,
            bool usbmux = true, bool mobdev2 = true)
        {
            /* TODO
    """ Start Tunneld service for remote tunneling """
    if not verify_tunnel_imports():
        return
    protocol = TunnelProtocol(protocol)
    tunneld_runner = partial(TunneldRunner.create, host, port, protocol=protocol, usb_monitor=usb, wifi_monitor=wifi,
                             usbmux_monitor=usbmux, mobdev2_monitor=mobdev2)
    if daemonize:
        try:
            from daemonize import Daemonize
        except ImportError:
            raise NotImplementedError('daemonizing is only supported on unix platforms')
        with tempfile.NamedTemporaryFile('wt') as pid_file:
            daemon = Daemonize(app=f'Tunneld {host}:{port}', pid=pid_file.name,
                               action=tunneld_runner)
            logger.info(f'starting Tunneld {host}:{port}')
            daemon.start()
    else:
        tunneld_runner()

    */
        }
    }

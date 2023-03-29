using Netimobiledevice.Exceptions;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Netimobiledevice.Usbmuxd
{
    /// <summary>
    /// Creates a socket connection to usbmuxd and handles the sending/recieving of packets. 
    /// For Max/Linux this is a unix domain socket, and for Windows it is a TCP socket. 
    /// </summary>
    internal class UsbmuxdSocket
    {
        private const string USBMUXD_SOCKET_FILE = "/var/run/usbmuxd";
        private const string USBMUXD_SOCKET_IP = "127.0.0.1";
        private const int USBMUXD_SOCKET_PORT = 27015;

        private readonly Socket socket;

        public UsbmuxdSocket()
        {
            try {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                    EndPoint windowsSocketAddress = new IPEndPoint(IPAddress.Parse(USBMUXD_SOCKET_IP), USBMUXD_SOCKET_PORT);
                    socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
                    socket.Connect(windowsSocketAddress);
                }
                else {
                    UnixDomainSocketEndPoint unixSocketAddress = new UnixDomainSocketEndPoint(USBMUXD_SOCKET_FILE);
                    socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
                    socket.Connect(unixSocketAddress);
                }
            }
            catch (SocketException ex) {
                throw new UsbmuxConnectionException("Can't create and/or connect to Usbmux socket", ex);
            }
        }

        public void Close()
        {
            socket.Close();
        }

        public Socket GetInternalSocket()
        {
            return socket;
        }

        public byte[] Receive(int size)
        {
            byte[] buf = new byte[size];
            socket.Receive(buf);
            return buf;
        }

        public int Send(byte[] message)
        {
            return socket.Send(message);
        }

        public void SetBlocking(bool blocking)
        {
            socket.Blocking = blocking;
        }

        public void SetTimeout(int timeout)
        {
            socket.ReceiveTimeout = timeout;
            socket.SendTimeout = timeout;
        }
    }
}

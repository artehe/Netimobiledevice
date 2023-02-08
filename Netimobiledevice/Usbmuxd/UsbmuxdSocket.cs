using Netimobiledevice.Exceptions;
using Netimobiledevice.Plist;
using Netimobiledevice.Usbmuxd.Responses;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Netimobiledevice.Usbmuxd;

/// <summary>
/// Creates a socket connection to usbmuxd and handles the sending/recieving of packets. 
/// For Max/Linux this is a unix domain socket, and for Windows it is a TCP socket. 
/// </summary>
internal class UsbmuxdSocket
{
    private const string USBMUXD_SOCKET_FILE = "/var/run/usbmuxd";
    private const string USBMUXD_SOCKET_IP = "127.0.0.1";
    private const int USBMUXD_SOCKET_PORT = 27015;

    private static readonly EndPoint WindowsSocketAddress = new IPEndPoint(IPAddress.Parse(USBMUXD_SOCKET_IP), USBMUXD_SOCKET_PORT);
    private static readonly UnixDomainSocketEndPoint UnixSocketAddress = new UnixDomainSocketEndPoint(USBMUXD_SOCKET_FILE);

    private readonly Socket socket;

    private int SocketTimeout { get; set; } = 5000;
    public UsbmuxdVersion ProtocolVersion { get; }

    public UsbmuxdSocket(UsbmuxdVersion version)
    {
        ProtocolVersion = version;

        try {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
                socket.Connect(WindowsSocketAddress);
            }
            else {
                socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
                socket.Connect(UnixSocketAddress);
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

    public int ReceivePacket(out UsbmuxdHeader header, out byte[] payload)
    {
        payload = Array.Empty<byte>();
        header = new UsbmuxdHeader() {
            Length = 0,
            Message = 0,
            Tag = 0,
            Version = 0
        };
        byte[] headerBuffer = new byte[Marshal.SizeOf(header)];

        socket.ReceiveTimeout = SocketTimeout;
        int recievedLength = socket.Receive(headerBuffer, Marshal.SizeOf(header), SocketFlags.None);
        if (recievedLength < 0) {
            Debug.WriteLine($"Error receiving packet: {recievedLength}");
            return recievedLength;
        }
        if (recievedLength < Marshal.SizeOf(header)) {
            Debug.WriteLine($"Received packet is too small, got {recievedLength} bytes!");
            return recievedLength;
        }

        header = UsbmuxdHeader.FromBytes(headerBuffer);
        byte[] payloadLoc = Array.Empty<byte>();

        int payloadSize = header.Length - Marshal.SizeOf(header);
        if (payloadSize > 0) {
            payloadLoc = new byte[payloadSize];
            uint rsize = 0;
            do {
                socket.ReceiveTimeout = SocketTimeout;
                int res = socket.Receive(payloadLoc, payloadSize, SocketFlags.None);
                if (res < 0) {
                    break;
                }
                rsize += (uint) res;
            } while (rsize < payloadSize);
            if (rsize != payloadSize) {
                Debug.WriteLine($"Error receiving payload of size {payloadSize} (bytes received: {rsize})");
                throw new UsbmuxException("Bad Message");
            }
        }

        payload = payloadLoc;
        return header.Length;
    }

    public PlistResponse ReceivePlistResponse(int expectedTag)
    {
        int recieveLength = ReceivePacket(out UsbmuxdHeader header, out byte[] payload);
        if (recieveLength < 0) {
            throw new UsbmuxException();
        }
        else if (recieveLength < Marshal.SizeOf(header)) {
            throw new UsbmuxVersionException("Protocol error");
        }

        if (header.Message != UsbmuxdMessageType.Plist) {
            throw new UsbmuxException($"Received non-plist type {header}");
        }
        if (header.Tag != expectedTag) {
            throw new UsbmuxException($"Reply tag mismatch: expected {expectedTag}, got {header.Tag}");
        }

        PlistResponse response = new PlistResponse(header, payload);
        return response;
    }

    public int SendPacket(UsbmuxdMessageType message, int tag, List<byte> payload)
    {
        UsbmuxdHeader header = new UsbmuxdHeader {
            Length = Marshal.SizeOf(typeof(UsbmuxdHeader)),
            Version = ProtocolVersion,
            Message = message,
            Tag = tag
        };

        if (payload != null && payload.Count > 0) {
            header.Length += payload.Count;
        }

        int sent = socket.Send(header.GetBytes(), Marshal.SizeOf(header), SocketFlags.None);
        if (sent != Marshal.SizeOf(header)) {
            Debug.WriteLine($"ERROR: could not send packet header");
            return -1;
        }

        if (payload != null && payload.Count > 0) {
            int res = socket.Send(payload.ToArray(), payload.Count, SocketFlags.None);
            sent += res;
        }
        if (sent != (int) header.Length) {
            Debug.WriteLine($"ERROR: could not send whole packet (sent {sent} of {header.Length})");
            socket.Close();
            return -1;
        }

        return sent;
    }

    public int SendPlistPacket(int tag, PropertyNode message)
    {
        List<byte> payload = PropertyList.SaveAsByteArray(message, PlistFormat.Xml).ToList();
        return SendPacket(UsbmuxdMessageType.Plist, tag, payload);
    }

    public void SetTimeout(int timeout)
    {
        SocketTimeout = timeout;
        socket.ReceiveTimeout = timeout;
        socket.SendTimeout = timeout;
    }
}

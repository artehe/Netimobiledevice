using Netimobiledevice.Exceptions;
using Netimobiledevice.Plist;
using Netimobiledevice.Usbmuxd.Responses;

namespace Netimobiledevice.Usbmuxd;

internal abstract class UsbmuxConnection
{
    /// <summary>
    /// After initiating the "Connect" packet, this same socket will be used to transfer data into the service
    /// residing inside the target device. when this happens, we can no longer send/receive control commands to
    /// usbmux on same socket
    /// </summary>
    private readonly bool connected = false;

    protected UsbmuxdSocket Sock { get; }
    /// <summary>
    /// Message sequence number. Used when verifying the response matched the request
    /// </summary>
    protected int Tag { get; set; }
    public UsbmuxdVersion ProtocolVersion => Sock.ProtocolVersion;
    public List<UsbmuxdDevice> Devices { get; private set; } = new List<UsbmuxdDevice>();

    protected UsbmuxConnection(UsbmuxdSocket socket)
    {
        Sock = socket;
        Tag = 1;
    }

    /// <summary>
    /// Verify active state is in state for control messages
    /// </summary>
    /// <exception cref="UsbmuxException"></exception>
    protected void AssertNotConnected()
    {
        if (connected) {
            throw new UsbmuxException("Usbmux is connected, cannot issue control packets");
        }
    }

    protected void AddDevice(UsbmuxdDevice device)
    {
        Devices.Add(device);
    }

    protected void RemoveDevice(long deviceId)
    {
        Devices.RemoveAll(x => x.DeviceId == deviceId);
    }

    /// <summary>
    /// Close the current Usbmux socket/connection
    /// </summary>
    public void Close()
    {
        Sock.Close();
    }

    public static UsbmuxConnection Create()
    {
        // First attempt to connect with possibly the wrong version header (using Plist protocol)
        UsbmuxdSocket sock = new UsbmuxdSocket(UsbmuxdVersion.Plist);
        int tag = 1;

        PropertyNode plistMessage = new StringNode("ReadBUID");
        sock.SendPlistPacket(tag, plistMessage);
        PlistResponse response = sock.ReceivePlistResponse(tag);

        // If we sent a bad request, we should re-create the socket in the correct version this time
        sock.Close();
        if (response.Header.Version == UsbmuxdVersion.Binary) {
            sock = new UsbmuxdSocket(UsbmuxdVersion.Binary);
            return new BinaryUsbmuxConnection(sock);
        }
        else if (response.Header.Version == UsbmuxdVersion.Plist) {
            sock = new UsbmuxdSocket(UsbmuxdVersion.Plist);
            return new PlistMuxConnection(sock);
        }
        throw new UsbmuxVersionException($"Usbmuxd returned unsupported version: {response.Header.Version}");
    }

    /// <summary>
    /// Start listening for events of attached and detached devices
    /// </summary>
    public abstract UsbmuxdResult Listen();

    public (UsbmuxdHeader, byte[]) Receive(int expectedTag = -1)
    {
        AssertNotConnected();
        Sock.ReceivePacket(out UsbmuxdHeader header, out byte[] payload);
        if (expectedTag > -1 && header.Tag != expectedTag) {
            throw new UsbmuxException($"Reply tag mismatch expected {expectedTag} but got {header.Tag}");
        }
        return (header, payload);
    }

    /// <summary>
    /// Request an update to the current device list from Usbmux.
    /// </summary>
    /// <param name="timeout">Timeout for the connection in ms</param>
    /// <returns></returns>
    public abstract void UpdateDeviceList(int timeout = 5000);
}

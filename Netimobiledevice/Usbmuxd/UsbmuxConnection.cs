using Netimobiledevice.Exceptions;
using Netimobiledevice.Plist;

namespace Netimobiledevice.Usbmuxd;

internal abstract class UsbmuxConnection
{
    protected UsbmuxdSocket Sock { get; }
    /// <summary>
    /// Message sequence number. Used when verifying the response matched the request
    /// </summary>
    protected int Tag { get; set; }
    public List<UsbmuxdDevice> Devices { get; private set; } = new List<UsbmuxdDevice>();

    protected UsbmuxConnection(UsbmuxdSocket socket)
    {
        Sock = socket;
        Tag = 1;
    }

    protected void AddDevice(UsbmuxdDevice device)
    {
        Devices.Add(device);
    }

    protected void RemoveDevice(long deviceId)
    {
        foreach (UsbmuxdDevice device in Devices) {
            if (device.DeviceId == deviceId) {
                Devices.Remove(device);
                break;
            }
        }
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
        UsbmuxdResponse response = sock.ReceivePlistResponse(tag);

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
    /// Request an update to the current device list from Usbmux.
    /// </summary>
    /// <param name="timeout">Timeout for the connection in ms</param>
    /// <returns></returns>
    public abstract void UpdateDeviceList(int timeout = 5000);
}

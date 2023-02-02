using Netimobiledevice.Exceptions;

namespace Netimobiledevice.Usbmuxd;

internal class BinaryUsbmuxConnection : UsbmuxConnection
{
    /// <summary>
    /// After initiating the "Connect" packet, this same socket will be used to transfer data into the service
    /// residing inside the target device. when this happens, we can no longer send/receive control commands to
    /// usbmux on same socket
    /// </summary>
    private readonly bool connected = false;

    public BinaryUsbmuxConnection(UsbmuxdSocket sock) : base(sock) { }

    /// <summary>
    /// Verify active state is in state for control messages
    /// </summary>
    /// <exception cref="UsbmuxException"></exception>
    private void AssertNotConnected()
    {
        if (connected) {
            throw new UsbmuxException("Usbmux is connected, cannot issue control packets");
        }
    }

    /// <summary>
    /// Start listening for events of attached and detached devices
    /// </summary>
    private void Listen()
    {
        SendReceive(UsbmuxdMessageType.Listen);
    }

    private UsbmuxdResponse Receive(int expectedTag = -1)
    {
        AssertNotConnected();
        Sock.ReceivePacket(out UsbmuxdHeader header, out byte[] payload);
        if (expectedTag > -1 && header.Tag != expectedTag) {
            throw new UsbmuxException($"Reply tag mismatch expected {expectedTag} but got {header.Tag}");
        }
        return new UsbmuxdResponse(header, payload);
    }

    private void SendReceive(UsbmuxdMessageType messageType)
    {
        Sock.SendPacket(messageType, Tag, new List<byte>());
        UsbmuxdResponse response = Receive(Tag - 1);
        if (response.Header.Message != UsbmuxdMessageType.Result) {
            throw new UsbmuxException($"Unexpected message type received expected {UsbmuxdMessageType.Result} but got {response.Header.Message}");
        }

        byte[] resultData = response.Data;
        int resultInt = BitConverter.ToInt32(resultData);
        UsbmuxdResult result = (UsbmuxdResult) resultInt;
        if (result != UsbmuxdResult.Ok) {
            throw new UsbmuxException($"{messageType} failed with error code {result}");
        }
    }

    private void ReceiveDeviceStateUpdate()
    {
        UsbmuxdResponse response = Receive();
        if (response.Header.Message == UsbmuxdMessageType.Add) {
            // Old protocol only supported USB devices
            UsbmuxdDeviceRecord deviceRecord = UsbmuxdDeviceRecord.FromBytes(response.Data);
            UsbmuxdDevice usbmuxdDevice = new UsbmuxdDevice(deviceRecord.DeviceId, deviceRecord.SerialNumber, UsbmuxdConnectionType.Usb);
            AddDevice(usbmuxdDevice);
        }
        else if (response.Header.Message == UsbmuxdMessageType.Remove) {
            byte[] resultData = response.Data;
            int deviceId = BitConverter.ToInt32(resultData);
            RemoveDevice(deviceId);
        }
        else {
            throw new UsbmuxException($"Invalid packet type received: {response.Data}");
        }
    }

    public override void UpdateDeviceList(int timeout = 5000)
    {
        // Use timeout to wait for the device list to be fully populated
        AssertNotConnected();
        DateTime end = DateTime.Now + new TimeSpan(timeout * 1000 * 10);
        Listen();
        while (DateTime.Now < end) {
            Sock.SetTimeout((int) (end - DateTime.Now).TotalMilliseconds);
            try {
                ReceiveDeviceStateUpdate();
            }
            catch (Exception ex) {
                throw new UsbmuxException("Exception in listener socket", ex);
            }
        }
    }
}

using Microsoft.Extensions.Logging;
using Netimobiledevice.EndianBitConversion;
using Netimobiledevice.Exceptions;
using Netimobiledevice.Usbmuxd.Responses;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Usbmuxd;

internal class BinaryUsbmuxConnection(UsbmuxdSocket sock, ILogger? logger = null) : UsbmuxConnection(sock, UsbmuxdVersion.Binary, logger)
{
    private UsbmuxdResult SendReceive(UsbmuxdMessageType messageType)
    {
        SendPacket(messageType, Tag, []);
        (UsbmuxdHeader header, byte[] payload) = Receive(Tag - 1);

        if (header.Message != UsbmuxdMessageType.Result) {
            throw new UsbmuxException($"Unexpected message type received expected {UsbmuxdMessageType.Result} but got {header.Message}");
        }

        ResultResponse response = new ResultResponse(header, payload);
        if (response.Result != UsbmuxdResult.Ok) {
            throw new UsbmuxException($"{messageType} failed with error code {response.Result}");
        }
        return response.Result;
    }

    private async Task<UsbmuxdResult> SendReceiveAsync(UsbmuxdMessageType messageType, CancellationToken cancellationToken = default)
    {
        await SendPacketAsync(messageType, Tag, Array.Empty<byte>(), cancellationToken).ConfigureAwait(false);
        UsbmuxPacket packet = await ReceiveAsync(Tag - 1, cancellationToken).ConfigureAwait(false);

        if (packet.Header.Message != UsbmuxdMessageType.Result) {
            throw new UsbmuxException($"Unexpected message type received expected {UsbmuxdMessageType.Result} but got {packet.Header.Message}");
        }

        ResultResponse response = new ResultResponse(packet.Header, packet.Payload);
        if (response.Result != UsbmuxdResult.Ok) {
            throw new UsbmuxException($"{messageType} failed with error code {response.Result}");
        }
        return response.Result;
    }

    private void ReceiveDeviceStateUpdate()
    {
        (UsbmuxdHeader header, byte[] payload) = Receive();
        if (header.Message == UsbmuxdMessageType.Add) {
            // Old protocol only supported USB devices
            AddResponse response = new AddResponse(header, payload);
            UsbmuxdDevice usbmuxdDevice = new UsbmuxdDevice(response.DeviceRecord.DeviceId, response.DeviceRecord.SerialNumber, UsbmuxdConnectionType.Usb);
            AddDevice(usbmuxdDevice);
        }
        else if (header.Message == UsbmuxdMessageType.Remove) {
            RemoveResponse response = new RemoveResponse(header, payload);
            RemoveDevice(response.DeviceId);
        }
        else {
            throw new UsbmuxException($"Invalid packet type received: {header.Message}");
        }
    }

    protected override async Task RequestConnect(long deviceId, ushort port, CancellationToken cancellationToken = default)
    {
        byte[] message =
        [
            .. BitConverter.GetBytes((int) deviceId),
            .. BitConverter.GetBytes((int) EndianNetworkConverter.HostToNetworkOrder(port)),
            0x00,
            0x00,
        ];
        await SendPacketAsync(UsbmuxdMessageType.Connect, Tag, message, cancellationToken).ConfigureAwait(false);

        UsbmuxPacket packet = await ReceiveAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        if (packet.Header.Message != UsbmuxdMessageType.Result) {
            throw new UsbmuxException($"Unxepected message type received: {packet.Header.Message}");
        }

        ResultResponse response = new ResultResponse(packet.Header, packet.Payload);
        if (response.Result != UsbmuxdResult.Ok) {
            throw new UsbmuxException($"{UsbmuxdMessageType.Connect} failed with error code {response.Result}");
        }
    }

    public override UsbmuxdResult Listen()
    {
        return SendReceive(UsbmuxdMessageType.Listen);
    }

    public override async Task<UsbmuxdResult> ListenAsync(CancellationToken cancellationToken = default)
    {
        return await SendReceiveAsync(UsbmuxdMessageType.Listen, cancellationToken).ConfigureAwait(false);
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

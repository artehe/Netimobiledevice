using Netimobiledevice.EndianBitConversion;
using Netimobiledevice.Exceptions;
using Netimobiledevice.Usbmuxd.Responses;
using System;
using System.Collections.Generic;

namespace Netimobiledevice.Usbmuxd
{
    internal class BinaryUsbmuxConnection : UsbmuxConnection
    {
        public BinaryUsbmuxConnection(UsbmuxdSocket sock) : base(sock, UsbmuxdVersion.Binary) { }

        private UsbmuxdResult SendReceive(UsbmuxdMessageType messageType)
        {
            SendPacket(messageType, Tag, Array.Empty<byte>());
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

        protected override void RequestConnect(long deviceId, ushort port)
        {
            List<byte> message = new List<byte>();
            message.AddRange(BitConverter.GetBytes((int) deviceId));
            message.AddRange(BitConverter.GetBytes((int) EndianNetworkConverter.HostToNetworkOrder(port)));
            message.Add(0x00);
            message.Add(0x00);

            SendPacket(UsbmuxdMessageType.Connect, Tag, message.ToArray());

            (UsbmuxdHeader header, byte[]? payload) = Receive();
            if (header.Message != UsbmuxdMessageType.Result) {
                throw new UsbmuxException($"Unxepected message type received: {header.Message}");
            }

            ResultResponse response = new ResultResponse(header, payload);
            if (response.Result != UsbmuxdResult.Ok) {
                throw new UsbmuxException($"{UsbmuxdMessageType.Connect} failed with error code {response.Result}");
            }
        }

        public override UsbmuxdResult Listen()
        {
            return SendReceive(UsbmuxdMessageType.Listen);
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
}

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Netimobiledevice.Exceptions;
using Netimobiledevice.Extentions;
using Netimobiledevice.Plist;
using Netimobiledevice.Usbmuxd.Responses;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Netimobiledevice.Usbmuxd
{
    internal abstract class UsbmuxConnection
    {
        protected const int DEFAULT_CONNECTION_TIMEOUT = 5000;

        /// <summary>
        /// After initiating the "Connect" packet, this same socket will be used to transfer data into the service
        /// residing inside the target device. when this happens, we can no longer send/receive control commands to
        /// usbmux on same socket
        /// </summary>
        private bool connected;
        /// <summary>
        /// The internal logger
        /// </summary>
        private readonly ILogger logger;

        protected int connectionTimeout = DEFAULT_CONNECTION_TIMEOUT;

        protected UsbmuxdSocket Sock { get; }
        /// <summary>
        /// Message sequence number. Used when verifying the response matched the request
        /// </summary>
        protected int Tag { get; set; }
        public UsbmuxdVersion ProtocolVersion { get; }
        public List<UsbmuxdDevice> Devices { get; private set; } = new List<UsbmuxdDevice>();

        protected UsbmuxConnection(UsbmuxdSocket socket, UsbmuxdVersion protocolVersion, ILogger logger)
        {
            this.logger = logger;

            ProtocolVersion = protocolVersion;
            Sock = socket;
            Tag = 1;
        }

        protected UsbmuxConnection(UsbmuxdSocket socket, UsbmuxdVersion protocolVersion) : this(socket, protocolVersion, NullLogger.Instance) { }

        private int ReceivePacket(out UsbmuxdHeader header, out byte[] payload)
        {
            AssertNotConnected();

            payload = Array.Empty<byte>();
            header = new UsbmuxdHeader() {
                Length = 0,
                Message = 0,
                Tag = 0,
                Version = 0
            };

            Sock.SetTimeout(connectionTimeout);
            byte[] headerBuffer = Sock.Receive(Marshal.SizeOf(header));
            int recievedLength = headerBuffer.Length;
            if (recievedLength < 0) {
                logger.LogError($"Error receiving packet: {recievedLength}");
                return recievedLength;
            }
            if (recievedLength < Marshal.SizeOf(header)) {
                logger.LogError($"Received packet is too small, got {recievedLength} bytes!");
                return recievedLength;
            }

            header = StructExtentions.FromBytes<UsbmuxdHeader>(headerBuffer);
            byte[] payloadLoc = Array.Empty<byte>();

            int payloadSize = header.Length - Marshal.SizeOf(header);
            if (payloadSize > 0) {
                uint rsize = 0;
                do {
                    Sock.SetTimeout(connectionTimeout);
                    payloadLoc = Sock.Receive(payloadSize);
                    int res = payloadLoc.Length;
                    if (res < 0) {
                        break;
                    }
                    rsize += (uint) res;
                } while (rsize < payloadSize);
                if (rsize != payloadSize) {
                    logger.LogError($"Error receiving payload of size {payloadSize} (bytes received: {rsize})");
                    throw new UsbmuxException("Bad Message");
                }
            }

            payload = payloadLoc;
            return header.Length;
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

        protected void RemoveDevice(ulong deviceId)
        {
            Devices.RemoveAll(x => x.DeviceId == deviceId);
        }

        /// <summary>
        /// Initiate a "Connect" request to target port
        /// </summary>
        protected abstract void RequestConnect(ulong deviceId, ushort port);

        protected int SendPacket(UsbmuxdMessageType message, int tag, byte[] payload)
        {
            AssertNotConnected();

            UsbmuxdHeader header = new UsbmuxdHeader {
                Length = Marshal.SizeOf(typeof(UsbmuxdHeader)),
                Version = ProtocolVersion,
                Message = message,
                Tag = tag
            };

            if (payload != null && payload.Length > 0) {
                header.Length += payload.Length;
            }

            int sent = Sock.Send(header.GetBytes());
            if (sent != Marshal.SizeOf(header)) {
                logger.LogError($"ERROR: could not send packet header");
                return -1;
            }

            if (payload != null && payload.Length > 0) {
                int res = Sock.Send(payload);
                sent += res;
            }
            if (sent != header.Length) {
                logger.LogError($"ERROR: could not send whole packet (sent {sent} of {header.Length})");
                Sock.Close();
                return -1;
            }

            Tag++;
            return sent;
        }

        /// <summary>
        /// Close the current Usbmux socket/connection
        /// </summary>
        public void Close()
        {
            Sock.Close();
        }

        /// <summary>
        /// Connect to a relay port on target machine and get a raw python socket object for the connection
        /// </summary>
        /// <param name="device">The usbmux device to connect to</param>
        /// <param name="port">The port to connect to the device on</param>
        /// <returns></returns>
        public Socket Connect(UsbmuxdDevice device, ushort port)
        {
            RequestConnect(device.DeviceId, port);
            connected = true;
            return Sock.GetInternalSocket();
        }

        public static UsbmuxConnection Create()
        {
            // First attempt to connect with possibly the wrong version header (using Plist protocol)
            UsbmuxdSocket sock = new UsbmuxdSocket();
            PlistMuxConnection conn = new PlistMuxConnection(sock);
            int tag = 1;

            PropertyNode plistMessage = new StringNode("ReadBUID");
            conn.Send(plistMessage);
            PlistResponse response = conn.ReceivePlist(tag);

            // If we sent a bad request, we should re-create the socket in the correct version this time
            sock.Close();

            sock = new UsbmuxdSocket();
            if (response.Header.Version == UsbmuxdVersion.Binary) {
                return new BinaryUsbmuxConnection(sock);
            }
            else if (response.Header.Version == UsbmuxdVersion.Plist) {
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
            ReceivePacket(out UsbmuxdHeader header, out byte[] payload);
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
}

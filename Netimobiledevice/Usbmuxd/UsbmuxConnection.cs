using Microsoft.Extensions.Logging;
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
    internal abstract class UsbmuxConnection(UsbmuxdSocket socket, UsbmuxdVersion protocolVersion, ILogger logger) : IDisposable
    {
        protected const int DEFAULT_CONNECTION_TIMEOUT = 5000;

        /// <summary>
        /// After initiating the "Connect" packet, this same socket will be used to transfer data into the service
        /// residing inside the target device. when this happens, we can no longer send/receive control commands to
        /// usbmux on same socket
        /// </summary>
        private bool _connected;

        protected int connectionTimeout = DEFAULT_CONNECTION_TIMEOUT;

        /// <summary>
        /// The internal logger
        /// </summary>
        protected ILogger Logger { get; } = logger;
        protected UsbmuxdSocket Sock { get; } = socket;
        /// <summary>
        /// Message sequence number. Used when verifying the response matched the request
        /// </summary>
        protected int Tag { get; set; } = 1;
        public UsbmuxdVersion ProtocolVersion { get; } = protocolVersion;
        public List<UsbmuxdDevice> Devices { get; private set; } = [];

        public void Dispose()
        {
            Sock.Close();
        }

        private int ReceivePacket(out UsbmuxdHeader header, out byte[] payload)
        {
            AssertNotConnected();

            payload = [];
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
                Logger.LogError("Error receiving packet: {recievedLength}", recievedLength);
                return recievedLength;
            }
            if (recievedLength < Marshal.SizeOf(header)) {
                Logger.LogError("Received packet is too small, got {recievedLength} bytes!", recievedLength);
                return recievedLength;
            }

            header = StructExtentions.FromBytes<UsbmuxdHeader>(headerBuffer);
            byte[] payloadLoc = [];

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
                    Logger.LogError("Error receiving payload of size {payloadSize} (bytes received: {rsize})", payloadSize, rsize);
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
            if (_connected) {
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
                Logger.LogError($"ERROR: could not send packet header");
                return -1;
            }

            if (payload != null && payload.Length > 0) {
                int res = 0;
                try {
                    res = Sock.Send(payload);
                    sent += res;
                }
                catch (SocketException ex) {
                    throw new UsbmuxException($"Failed to send {payload.Length} bytes; actually sent {res}", ex);
                }
            }
            if (sent != header.Length) {
                Logger.LogError("Could not send whole packet (sent {sent} of {length})", sent, header.Length);
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
            _connected = true;
            return Sock.GetInternalSocket();
        }

        public static UsbmuxConnection Create(string usbmuxAddress = "", ILogger? logger = null)
        {
            // First attempt to connect with possibly the wrong version header (using Plist protocol)
            UsbmuxdSocket sock = new UsbmuxdSocket(usbmuxAddress: usbmuxAddress);

            PlistMuxConnection conn = new PlistMuxConnection(sock, logger);
            int tag = 1;

            DictionaryNode msg = new DictionaryNode() {
                { "MessageType", new StringNode("ReadBUID") }
            };
            conn.Send(msg);
            PlistResponse response = conn.ReceivePlist(tag);

            // If we sent a bad request, we should re-create the socket in the correct version this time
            sock.Close();

            sock = new UsbmuxdSocket(usbmuxAddress: usbmuxAddress);
            if (response.Header.Version == UsbmuxdVersion.Binary) {
                return new BinaryUsbmuxConnection(sock, logger);
            }
            else if (response.Header.Version == UsbmuxdVersion.Plist) {
                return new PlistMuxConnection(sock, logger);
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

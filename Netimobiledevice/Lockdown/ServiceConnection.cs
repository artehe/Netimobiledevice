﻿using Netimobiledevice.EndianBitConversion;
using Netimobiledevice.Exceptions;
using Netimobiledevice.Plist;
using Netimobiledevice.Usbmuxd;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Lockdown
{
    /// <summary>
    /// A wrapper for usbmux tcp-relay connections
    /// </summary>
    public class ServiceConnection : IDisposable
    {
        private const int MAX_READ_SIZE = 4096;

        private readonly UsbmuxdDevice? muxDevice;
        private Stream networkStream;

        private ServiceConnection(Socket sock, UsbmuxdDevice? muxDevice = null)
        {
            networkStream = new NetworkStream(sock, true);
            // Usbmux connections contain additional information associated with the current connection
            this.muxDevice = muxDevice;
        }

        private static ServiceConnection CreateUsingTcp(string hostname, ushort port)
        {
            Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            sock.Connect(hostname, port);
            return new ServiceConnection(sock);
        }

        private static ServiceConnection CreateUsingUsbmux(string udid, ushort port, UsbmuxdConnectionType? usbmuxdConnectionType = null)
        {
            UsbmuxdDevice? targetDevice = Usbmux.GetDevice(udid, usbmuxdConnectionType);
            if (targetDevice == null) {
                if (!string.IsNullOrEmpty(udid)) {
                    throw new ConnectionFailedException();
                }
                throw new NoDeviceConnectedException();
            }
            Socket sock = targetDevice.Connect(port);
            return new ServiceConnection(sock, targetDevice);
        }

        private async Task<byte[]> Receive(int size, CancellationToken cancellationToken)
        {
            if (size <= 0) {
                return Array.Empty<byte>();
            }
            List<byte> buffer = new List<byte>();

            int totalBytesRead = 0;
            while (totalBytesRead < size) {
                byte[] buf;
                if (size - totalBytesRead > MAX_READ_SIZE) {
                    buf = new byte[MAX_READ_SIZE];
                }
                else {
                    buf = new byte[size];
                }

                int bytesRead;
                if (networkStream.ReadTimeout != -1) {
                    Task<int> result = networkStream.ReadAsync(buf, 0, buf.Length, cancellationToken);
                    await Task.WhenAny(result, Task.Delay(networkStream.ReadTimeout, cancellationToken));
                    if (!result.IsCompleted) {
                        throw new TimeoutException("Timeout waiting for message from service");
                    }
                    bytesRead = await result;
                }
                else {
                    bytesRead = await networkStream.ReadAsync(buf, totalBytesRead, size - totalBytesRead, cancellationToken);
                }

                totalBytesRead += bytesRead;
                buffer.AddRange(buf.Take(bytesRead));
            }

            return buffer.ToArray();
        }

        /// <summary>
        /// Receive a data block prefixed with a u32 length field
        /// </summary>
        /// <returns>The data without the u32 field length as a byte array</returns>
        private async Task<byte[]> ReceivePrefixed(CancellationToken cancellationToken)
        {
            byte[] sizeBytes = await Receive(4, cancellationToken);
            if (sizeBytes.Length != 4) {
                return Array.Empty<byte>();
            }

            int size = EndianBitConverter.BigEndian.ToInt32(sizeBytes, 0);
            return await Receive(size, cancellationToken);
        }

        private bool UserCertificateValidationCallback(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        public void Close()
        {
            networkStream.Close();
        }

        public static ServiceConnection Create(ConnectionMedium medium, string identifier, ushort port, UsbmuxdConnectionType? usbmuxdConnectionType = null)
        {
            if (medium == ConnectionMedium.TCP) {
                return CreateUsingTcp(identifier, port);
            }
            else {
                return CreateUsingUsbmux(identifier, port, usbmuxdConnectionType);
            }
        }

        public void Dispose()
        {
            Close();
            networkStream.Dispose();
            GC.SuppressFinalize(this);
        }

        public UsbmuxdDevice? GetUsbmuxdDevice()
        {
            return muxDevice;
        }

        public byte[] Receive(int length = 4096)
        {
            return Receive(length, CancellationToken.None).GetAwaiter().GetResult();
        }

        public async Task<byte[]> ReceiveAsync(int length = 4096)
        {
            return await Receive(length, CancellationToken.None);
        }

        public async Task<PropertyNode?> ReceivePlist()
        {
            byte[] plistBytes = await ReceivePrefixed(CancellationToken.None);
            if (plistBytes.Length == 0) {
                return null;
            }
            return await PropertyList.LoadFromByteArrayAsync(plistBytes);
        }

        public void Send(byte[] data)
        {
            networkStream.Write(data);
        }

        public async Task SendAsync(byte[] data)
        {
            await networkStream.WriteAsync(data);
        }

        public void SendPlist(PropertyNode data, PlistFormat format = PlistFormat.Xml)
        {
            byte[] plistBytes = PropertyList.SaveAsByteArray(data, format);
            byte[] lengthBytes = BitConverter.GetBytes(EndianBitConverter.BigEndian.ToInt32(BitConverter.GetBytes(plistBytes.Length), 0));

            List<byte> payload = new List<byte>();
            payload.AddRange(lengthBytes);
            payload.AddRange(plistBytes);
            Send(payload.ToArray());
        }

        public async Task SendPlistAsync(PropertyNode data)
        {
            byte[] plistBytes = PropertyList.SaveAsByteArray(data, PlistFormat.Xml);
            byte[] lengthBytes = BitConverter.GetBytes(EndianBitConverter.BigEndian.ToInt32(BitConverter.GetBytes(plistBytes.Length), 0));

            List<byte> payload = new List<byte>();
            payload.AddRange(lengthBytes);
            payload.AddRange(plistBytes);
            await SendAsync(payload.ToArray());
        }

        public PropertyNode? SendReceivePlist(PropertyNode data)
        {
            SendPlist(data);
            return ReceivePlist().GetAwaiter().GetResult();
        }

        public async Task<PropertyNode?> SendReceivePlistAsync(PropertyNode data)
        {
            await SendPlistAsync(data);
            return await ReceivePlist();
        }

        /// <summary>
        /// Set a value in milliseconds, that determines how long the service connection will attempt to read/write for before timing out
        /// </summary>
        /// <param name="timeout">A value in milliseconds that detemines how long the service connection will wait before timing out</param>
        public void SetTimeout(int timeout = -1)
        {
            networkStream.ReadTimeout = timeout;
            networkStream.WriteTimeout = timeout;
        }

        public void StartSSL(byte[] certData, byte[] privateKeyData)
        {
            X509Certificate2 cert;
            string tmpPath = Path.GetTempFileName();
            using (FileStream fs = new FileStream(tmpPath, FileMode.OpenOrCreate, FileAccess.ReadWrite)) {
                fs.Write(certData);
                fs.Write(Encoding.UTF8.GetBytes("\n"));
                fs.Write(privateKeyData);
            }
            cert = X509Certificate2.CreateFromPemFile(tmpPath);

            networkStream.Flush();

            SslStream sslStream = new SslStream(networkStream, true, UserCertificateValidationCallback, null, EncryptionPolicy.AllowNoEncryption);
            try {
                // NOTE: For some reason we need to re-export and then import the cert again ¯\_(ツ)_/¯
                // see this for more details: https://github.com/dotnet/runtime/issues/45680
                sslStream.AuthenticateAsClient(string.Empty, new X509CertificateCollection() { new X509Certificate2(cert.Export(X509ContentType.Pkcs12)) }, SslProtocols.None, false);
            }
            catch (AuthenticationException ex) {
                Debug.WriteLine(ex);
                Debug.WriteLine("SSL authentication failed");
            }

            networkStream = sslStream;
        }
    }
}

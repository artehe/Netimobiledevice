using Microsoft.Extensions.Logging;
using Netimobiledevice.EndianBitConversion;
using Netimobiledevice.Exceptions;
using Netimobiledevice.Plist;
using Netimobiledevice.Usbmuxd;
using System;
using System.Collections.Generic;
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

        /// <summary>
        /// The internal logger
        /// </summary>
        private readonly ILogger logger;
        private Stream networkStream;
        private readonly byte[] receiveBuffer = new byte[MAX_READ_SIZE];

        public UsbmuxdDevice? MuxDevice { get; private set; }

        private ServiceConnection(Socket sock, ILogger logger, UsbmuxdDevice? muxDevice = null)
        {
            this.logger = logger;

            networkStream = new NetworkStream(sock, true);
            // Usbmux connections contain additional information associated with the current connection
            MuxDevice = muxDevice;
        }

        internal static ServiceConnection CreateUsingTcp(string hostname, ushort port, ILogger? logger = null)
        {
            Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            sock.Connect(hostname, port);
            return new ServiceConnection(sock, logger);
        }

        internal static ServiceConnection CreateUsingUsbmux(string udid, ushort port, UsbmuxdConnectionType? connectionType = null, string usbmuxAddress = "", ILogger? logger = null)
        {
            UsbmuxdDevice? targetDevice = Usbmux.GetDevice(udid, connectionType: connectionType, usbmuxAddress: usbmuxAddress);
            if (targetDevice == null) {
                if (!string.IsNullOrEmpty(udid)) {
                    throw new ConnectionFailedException();
                }
                throw new NoDeviceConnectedException();
            }
            Socket sock = targetDevice.Connect(port, usbmuxAddress: usbmuxAddress, logger);
            return new ServiceConnection(sock, logger, targetDevice);
        }

        private bool UserCertificateValidationCallback(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        public void Close()
        {
            networkStream.Close();
        }

        public void Dispose()
        {
            Close();
            networkStream.Dispose();
            GC.SuppressFinalize(this);
        }

        public byte[] Receive(int length = 4096)
        {
            if (length <= 0) {
                return Array.Empty<byte>();
            }
            List<byte> buffer = new List<byte>();

            int totalBytesRead = 0;
            while (totalBytesRead < length) {
                int remainingSize = length - totalBytesRead;
                int readSize = remainingSize;
                if (remainingSize > MAX_READ_SIZE) {
                    readSize = MAX_READ_SIZE;
                }

                int bytesRead = networkStream.Read(receiveBuffer, 0, readSize);
                totalBytesRead += bytesRead;

                buffer.AddRange(receiveBuffer.Take(bytesRead));
            }

            return buffer.ToArray();
        }

        public async Task<byte[]> ReceiveAsync(int length, CancellationToken cancellationToken)
        {
            if (length <= 0) {
                return Array.Empty<byte>();
            }
            List<byte> buffer = new List<byte>();

            int totalBytesRead = 0;
            while (totalBytesRead < length) {
                int remainingSize = length - totalBytesRead;
                int readSize = remainingSize;
                if (remainingSize > MAX_READ_SIZE) {
                    readSize = MAX_READ_SIZE;
                }

                int bytesRead;
                if (networkStream.ReadTimeout != -1) {
                    CancellationTokenSource localTaskComplete = new CancellationTokenSource();

                    Task<int> result = networkStream.ReadAsync(receiveBuffer, 0, readSize, localTaskComplete.Token);
                    Task delay = Task.Delay(networkStream.ReadTimeout, localTaskComplete.Token);

                    await Task.WhenAny(result, delay).WaitAsync(cancellationToken);
                    if (cancellationToken.IsCancellationRequested) {
                        localTaskComplete.Cancel();
                    }
                    else if (!result.IsCompleted) {
                        localTaskComplete.Cancel();
                        throw new TimeoutException("Timeout waiting for message from service");
                    }
                    bytesRead = await result;
                }
                else {
                    bytesRead = await networkStream.ReadAsync(receiveBuffer.AsMemory(0, readSize), cancellationToken);
                }

                totalBytesRead += bytesRead;
                buffer.AddRange(receiveBuffer.Take(bytesRead));
            }

            return buffer.ToArray();
        }

        public PropertyNode? ReceivePlist()
        {
            byte[] plistBytes = ReceivePrefixed();
            if (plistBytes.Length == 0) {
                return null;
            }
            return PropertyList.LoadFromByteArray(plistBytes);
        }

        public async Task<PropertyNode?> ReceivePlistAsync(CancellationToken cancellationToken)
        {
            byte[] plistBytes = await ReceivePrefixedAsync(cancellationToken);
            if (plistBytes.Length == 0) {
                return null;
            }
            return await PropertyList.LoadFromByteArrayAsync(plistBytes);
        }

        /// <summary>
        /// Receive a data block prefixed with a u32 length field
        /// </summary>
        /// <returns>The data without the u32 field length as a byte array</returns>
        public byte[] ReceivePrefixed()
        {
            byte[] sizeBytes = Receive(4);
            if (sizeBytes.Length != 4) {
                return Array.Empty<byte>();
            }

            int size = EndianBitConverter.BigEndian.ToInt32(sizeBytes, 0);
            return Receive(size);
        }

        /// <summary>
        /// Receive a data block prefixed with a u32 length field
        /// </summary>
        /// <returns>The data without the u32 field length as a byte array</returns>
        public async Task<byte[]> ReceivePrefixedAsync(CancellationToken cancellationToken = default)
        {
            byte[] sizeBytes = await ReceiveAsync(4, cancellationToken);
            if (sizeBytes.Length != 4) {
                return Array.Empty<byte>();
            }

            int size = EndianBitConverter.BigEndian.ToInt32(sizeBytes, 0);
            return await ReceiveAsync(size, cancellationToken);
        }

        public void Send(byte[] data)
        {
            networkStream.Write(data);
        }

        public async Task SendAsync(byte[] data, CancellationToken cancellationToken)
        {
            await networkStream.WriteAsync(data, cancellationToken);
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

        public async Task SendPlistAsync(PropertyNode data, CancellationToken cancellationToken)
        {
            byte[] plistBytes = PropertyList.SaveAsByteArray(data, PlistFormat.Xml);
            byte[] lengthBytes = BitConverter.GetBytes(EndianBitConverter.BigEndian.ToInt32(BitConverter.GetBytes(plistBytes.Length), 0));

            List<byte> payload = new List<byte>();
            payload.AddRange(lengthBytes);
            payload.AddRange(plistBytes);
            await SendAsync(payload.ToArray(), cancellationToken);
        }

        public PropertyNode? SendReceivePlist(PropertyNode data)
        {
            SendPlist(data);
            return ReceivePlist();
        }

        public async Task<PropertyNode?> SendReceivePlistAsync(PropertyNode data, CancellationToken cancellationToken)
        {
            await SendPlistAsync(data, cancellationToken);
            return await ReceivePlistAsync(cancellationToken);
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

            SslStream sslStream = new SslStream(networkStream, true, UserCertificateValidationCallback, null, EncryptionPolicy.RequireEncryption);
            try {
                // NOTE: For some reason we need to re-export and then import the cert again ¯\_(ツ)_/¯
                // see this for more details: https://github.com/dotnet/runtime/issues/45680
                sslStream.AuthenticateAsClient(string.Empty, new X509CertificateCollection() { new X509Certificate2(cert.Export(X509ContentType.Pkcs12)) }, SslProtocols.None, false);
            }
            catch (AuthenticationException ex) {
                logger.LogError($"SSL authentication failed: {ex}");
            }

            networkStream = sslStream;
        }
    }
}

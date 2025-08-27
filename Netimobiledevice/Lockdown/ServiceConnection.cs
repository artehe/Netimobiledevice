using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Netimobiledevice.EndianBitConversion;
using Netimobiledevice.Exceptions;
using Netimobiledevice.Plist;
using Netimobiledevice.Usbmuxd;
using System;
using System.IO;
using System.Linq;
using System.Net;
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
        private const int MAX_READ_SIZE = 32768;

        /// <summary>
        /// The internal logger
        /// </summary>
        private readonly ILogger _logger;
        /// <summary>
        /// The initial stream used for the ServiceConnection until the SSL stream starts, unless you specifically need to use this stream
        /// you should use the Stream property instead
        /// </summary>
        private readonly NetworkStream _networkStream;
        /// <summary>
        /// The main stream once SSL is established, unless you specifically need to use this stream you should use the Stream 
        /// property instead
        /// </summary>
        private SslStream? _sslStream;

        public UsbmuxdDevice? MuxDevice { get; private set; }

        public bool IsConnected {
            get {
                return _networkStream.Socket.Connected;
            }
        }

        public Stream Stream => _sslStream != null ? _sslStream : _networkStream;

        private ServiceConnection(Socket sock, ILogger logger, UsbmuxdDevice? muxDevice = null)
        {
            _logger = logger;
            _networkStream = new NetworkStream(sock, true);

            // Usbmux connections contain additional information associated with the current connection
            MuxDevice = muxDevice;
        }

        internal static ServiceConnection CreateUsingTcp(string hostname, ushort port, ILogger? logger = null)
        {
            IPAddress ip = IPAddress.Parse(hostname);
            Socket sock = new Socket(SocketType.Stream, ProtocolType.IP);
            sock.Connect(ip, port);
            return new ServiceConnection(sock, logger ?? NullLogger.Instance);
        }

        internal static async Task<ServiceConnection> CreateUsingUsbmux(string udid, ushort port, UsbmuxdConnectionType? connectionType = null, string usbmuxAddress = "", ILogger? logger = null)
        {
            UsbmuxdDevice? targetDevice = Usbmux.GetDevice(udid, connectionType: connectionType, usbmuxAddress: usbmuxAddress);
            if (targetDevice == null) {
                if (!string.IsNullOrEmpty(udid)) {
                    throw new ConnectionFailedException();
                }
                throw new NoDeviceConnectedException();
            }
            Socket sock = await targetDevice.Connect(port, usbmuxAddress: usbmuxAddress, logger).ConfigureAwait(false);
            return new ServiceConnection(sock, logger ?? NullLogger.Instance, targetDevice);
        }

        private bool UserCertificateValidationCallback(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        public void Close()
        {
            Stream.Close();
        }

        public void Dispose()
        {
            Close();
            Stream.Dispose();
            GC.SuppressFinalize(this);
        }

        public byte[] Receive(int length = 4096)
        {
            if (length <= 0) {
                return [];
            }
            byte[] buffer = new byte[length];

            int totalBytesRead = 0;
            while (totalBytesRead < length) {
                int remainingSize = length - totalBytesRead;
                int readSize = remainingSize;
                if (remainingSize > MAX_READ_SIZE) {
                    readSize = MAX_READ_SIZE;
                }

                int bytesRead = Stream.Read(buffer, totalBytesRead, readSize);
                if (bytesRead == 0) {
                    _logger.LogError("Read zero bytes so the connection has been broken");
                    break;
                }
                totalBytesRead += bytesRead;
            }

            if (totalBytesRead < buffer.Length) {
                return [.. buffer.Take(totalBytesRead)];
            }
            return buffer;
        }

        public async Task<byte[]> ReceiveAsync(int length, CancellationToken cancellationToken)
        {
            if (length <= 0) {
                return [];
            }
            byte[] buffer = new byte[length];

            int totalBytesRead = 0;
            while (totalBytesRead < length) {
                int remainingSize = length - totalBytesRead;
                int readSize = remainingSize;
                if (remainingSize > MAX_READ_SIZE) {
                    readSize = MAX_READ_SIZE;
                }

                int bytesRead;
                if (Stream.ReadTimeout != -1) {
                    CancellationTokenSource localTaskComplete = new CancellationTokenSource(Stream.ReadTimeout);
                    CancellationTokenSource linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(localTaskComplete.Token, cancellationToken);
                    try {
                        bytesRead = await Stream.ReadAsync(buffer.AsMemory(totalBytesRead, readSize), linkedCancellationTokenSource.Token).ConfigureAwait(false);
                        if (bytesRead == 0) {
                            _logger.LogError("Read zero bytes so the connection has been broken");
                            break;
                        }
                    }
                    catch (OperationCanceledException) {
                        if (localTaskComplete.IsCancellationRequested) {
                            throw new TimeoutException("Timeout waiting for message from service");
                        }
                        throw;
                    }
                }
                else {
                    bytesRead = await Stream.ReadAsync(buffer.AsMemory(totalBytesRead, readSize), cancellationToken).ConfigureAwait(false);
                }

                totalBytesRead += bytesRead;
            }

            if (totalBytesRead < buffer.Length) {
                return [.. buffer.Take(totalBytesRead)];
            }
            return buffer;
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
            byte[] plistBytes = await ReceivePrefixedAsync(cancellationToken).ConfigureAwait(false);
            if (plistBytes.Length == 0) {
                return null;
            }
            return await PropertyList.LoadFromByteArrayAsync(plistBytes).ConfigureAwait(false);
        }

        /// <summary>
        /// Receive a data block prefixed with a u32 length field
        /// </summary>
        /// <returns>The data without the u32 field length as a byte array</returns>
        public byte[] ReceivePrefixed()
        {
            byte[] sizeBytes = Receive(4);
            if (sizeBytes.Length != 4) {
                return [];
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
            byte[] sizeBytes = await ReceiveAsync(4, cancellationToken).ConfigureAwait(false);
            if (sizeBytes.Length != 4) {
                return [];
            }

            int size = EndianBitConverter.BigEndian.ToInt32(sizeBytes, 0);
            return await ReceiveAsync(size, cancellationToken).ConfigureAwait(false);
        }

        public void Send(byte[] data)
        {
            Stream.Write(data);
        }

        public async Task SendAsync(byte[] data, CancellationToken cancellationToken)
        {
            await Stream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
        }

        public void SendPlist(PropertyNode data, PlistFormat format = PlistFormat.Xml)
        {
            byte[] plistBytes = PropertyList.SaveAsByteArray(data, format);
            byte[] lengthBytes = BitConverter.GetBytes(EndianBitConverter.BigEndian.ToInt32(BitConverter.GetBytes(plistBytes.Length), 0));

            Send(lengthBytes);
            Send(plistBytes);
        }

        public async Task SendPlistAsync(PropertyNode data, PlistFormat format = PlistFormat.Xml, CancellationToken cancellationToken = default)
        {
            byte[] plistBytes = PropertyList.SaveAsByteArray(data, format);
            byte[] lengthBytes = BitConverter.GetBytes(EndianBitConverter.BigEndian.ToInt32(BitConverter.GetBytes(plistBytes.Length), 0));

            await SendAsync(lengthBytes, cancellationToken).ConfigureAwait(false);
            await SendAsync(plistBytes, cancellationToken).ConfigureAwait(false);
        }

        public PropertyNode? SendReceivePlist(PropertyNode data)
        {
            SendPlist(data);
            return ReceivePlist();
        }

        public async Task<PropertyNode?> SendReceivePlistAsync(PropertyNode data, CancellationToken cancellationToken)
        {
            await SendPlistAsync(data, cancellationToken: cancellationToken).ConfigureAwait(false);
            return await ReceivePlistAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Set a value in milliseconds, that determines how long the service connection will attempt to read/write for before timing out
        /// </summary>
        /// <param name="timeout">A value in milliseconds that detemines how long the service connection will wait before timing out</param>
        public void SetTimeout(int timeout = -1)
        {
            Stream.ReadTimeout = timeout;
            Stream.WriteTimeout = timeout;
        }

        public void StartSSL(byte[] certData, byte[] privateKeyData)
        {
            string certText = Encoding.UTF8.GetString(certData);
            string privateKeyText = Encoding.UTF8.GetString(privateKeyData);
            X509Certificate2 cert = X509Certificate2.CreateFromPem(certText, privateKeyText);

            if (_networkStream == null) {
                throw new InvalidOperationException("Network stream is null");
            }
            _networkStream.Flush();

            _sslStream = new SslStream(_networkStream, true, UserCertificateValidationCallback, null, EncryptionPolicy.RequireEncryption);
            try {
                // NOTE: For some reason we need to re-export and then import the cert again ¯\_(ツ)_/¯
                // see this for more details: https://github.com/dotnet/runtime/issues/45680
                _sslStream.AuthenticateAsClient(string.Empty, [new X509Certificate2(cert.Export(X509ContentType.Pkcs12))], SslProtocols.None, false);
            }
            catch (AuthenticationException ex) {
                _logger.LogError(ex, "SSL authentication failed");
            }
        }
    }
}

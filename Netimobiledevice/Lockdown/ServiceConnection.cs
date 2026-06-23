using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Netimobiledevice.Plist;
using Netimobiledevice.Usbmuxd;
using System;
using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Lockdown;

/// <summary>
/// A wrapper for usbmux tcp-relay connections
/// </summary>
public sealed class ServiceConnection : IDisposable, IAsyncDisposable {
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
    private int _timeout;

    public UsbmuxdDevice? MuxDevice { get; private set; }

    public bool IsConnected {
        get {
            return _networkStream.Socket.Connected;
        }
    }

    public Stream Stream => _sslStream != null ? _sslStream : _networkStream;

    private ServiceConnection(Socket sock, int timeout, ILogger logger, UsbmuxdDevice? muxDevice = null) {
        _logger = logger;
        _timeout = timeout;
        _networkStream = new NetworkStream(sock, true) {
            ReadTimeout = _timeout,
            WriteTimeout = _timeout
        };

        // Usbmux connections contain additional information associated with the current connection
        MuxDevice = muxDevice;
    }

    internal static ServiceConnection CreateUsingTcp(string hostname, ushort port, int timeout = 10_000, ILogger? logger = null) {
        IPAddress ip = IPAddress.Parse(hostname);
        Socket sock = new Socket(SocketType.Stream, ProtocolType.Tcp);
        sock.Connect(ip, port);
        return new ServiceConnection(sock, timeout, logger ?? NullLogger.Instance);
    }

    internal static async Task<ServiceConnection> CreateUsingTcpAsync(string hostname, ushort port, int timeout = 10_000, ILogger? logger = null) {
        IPAddress ip = IPAddress.Parse(hostname);
        Socket sock = new Socket(SocketType.Stream, ProtocolType.Tcp);

        using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeout))) {
            try {
                await sock.ConnectAsync(ip, port, cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) {
                sock.Dispose();
                throw new SocketException((int) SocketError.TimedOut);
            }
            catch {
                sock.Dispose();
                throw;
            }
        }

        return new ServiceConnection(sock, timeout, logger ?? NullLogger.Instance);
    }

    internal static ServiceConnection CreateUsingUsbmux(string udid, ushort port, UsbmuxdConnectionType? connectionType = null, string usbmuxAddress = "", int timeout = 10_000, ILogger? logger = null) {
        UsbmuxdDevice? targetDevice = Usbmux.GetDevice(udid, connectionType: connectionType, usbmuxAddress: usbmuxAddress);
        if (targetDevice == null) {
            if (!string.IsNullOrEmpty(udid)) {
                throw new ConnectionFailedException();
            }
            throw new NoDeviceConnectedException();
        }
        Socket sock = targetDevice.Connect(port, usbmuxAddress: usbmuxAddress, logger);
        return new ServiceConnection(sock, timeout, logger ?? NullLogger.Instance, targetDevice);
    }

    internal static async Task<ServiceConnection> CreateUsingUsbmuxAsync(string udid, ushort port, UsbmuxdConnectionType? connectionType = null, string usbmuxAddress = "", int timeout = 10_000, ILogger? logger = null) {
        UsbmuxdDevice? targetDevice = Usbmux.GetDevice(udid, connectionType: connectionType, usbmuxAddress: usbmuxAddress);
        if (targetDevice == null) {
            if (!string.IsNullOrEmpty(udid)) {
                throw new ConnectionFailedException();
            }
            throw new NoDeviceConnectedException();
        }
        Socket sock = await targetDevice.ConnectAsync(port, usbmuxAddress: usbmuxAddress, logger).ConfigureAwait(false);
        return new ServiceConnection(sock, timeout, logger ?? NullLogger.Instance, targetDevice);
    }

    /// <summary>
    /// iOS pairing uses self-signed/host-issued certs that can't be chain-validated so we have this function which always returns true to ignore that
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="certificate"></param>
    /// <param name="chain"></param>
    /// <param name="sslPolicyErrors"></param>
    /// <returns></returns>
    private bool UserCertificateValidationCallback(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors) {
        return true;
    }

    public void Close() {
        _sslStream?.Close();
        _networkStream.Close();
    }

    public void Dispose() {
        Close();

        _sslStream?.Dispose();
        _networkStream.Dispose();

        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync() {
        Close();

        if (_sslStream != null) {
            await _sslStream.DisposeAsync().ConfigureAwait(false);
        }
        await _networkStream.DisposeAsync().ConfigureAwait(false);

        GC.SuppressFinalize(this);
    }

    public byte[] Receive(int length = 4096) {
        if (length <= 0) {
            return [];
        }

        byte[] buffer = new byte[length];
        Stream.ReadExactly(buffer);
        return buffer;
    }

    public async Task<byte[]> ReceiveAsync(int length, CancellationToken cancellationToken) {
        if (length <= 0) {
            return [];
        }

        byte[] buffer = new byte[length];

        TimeSpan timeout = Stream.ReadTimeout == Timeout.Infinite ? Timeout.InfiniteTimeSpan : TimeSpan.FromMilliseconds(Stream.ReadTimeout);
        using (CancellationTokenSource timeoutCts = new CancellationTokenSource(timeout)) {
            using (CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken)) {
                try {
                    await Stream.ReadExactlyAsync(buffer, linkedCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested) {
                    throw new TimeoutException("Timeout waiting for message from service");
                }
            }
        }
        return buffer;
    }

    public PropertyNode? ReceivePlist() {
        byte[] plistBytes = ReceivePrefixed();
        if (plistBytes.Length == 0) {
            return null;
        }
        return PropertyList.LoadFromByteArray(plistBytes);
    }

    public async Task<PropertyNode?> ReceivePlistAsync(CancellationToken cancellationToken) {
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
    public byte[] ReceivePrefixed() {
        byte[] sizeBytes = Receive(4);
        if (sizeBytes.Length != 4) {
            return [];
        }

        int size = BinaryPrimitives.ReadInt32BigEndian(sizeBytes);
        return Receive(size);
    }

    /// <summary>
    /// Receive a data block prefixed with a u32 length field
    /// </summary>
    /// <returns>The data without the u32 field length as a byte array</returns>
    public async Task<byte[]> ReceivePrefixedAsync(CancellationToken cancellationToken = default) {
        byte[] sizeBytes = await ReceiveAsync(4, cancellationToken).ConfigureAwait(false);
        if (sizeBytes.Length != 4) {
            return [];
        }

        int size = BinaryPrimitives.ReadInt32BigEndian(sizeBytes);
        return await ReceiveAsync(size, cancellationToken).ConfigureAwait(false);
    }

    public void Send(ReadOnlySpan<byte> data) {
        Stream.Write(data);
    }

    public async Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken) {
        await Stream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
    }

    public void SendPlist(PropertyNode data, PlistFormat format = PlistFormat.Xml) {
        byte[] plistBytes = PropertyList.SaveAsByteArray(data, format);

        byte[] lengthBytes = new byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32BigEndian(lengthBytes, (uint) plistBytes.Length);

        Send(lengthBytes);
        Send(plistBytes);
    }

    public async Task SendPlistAsync(PropertyNode data, PlistFormat format = PlistFormat.Xml, CancellationToken cancellationToken = default) {
        byte[] plistBytes = PropertyList.SaveAsByteArray(data, format);

        byte[] lengthBytes = new byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32BigEndian(lengthBytes, (uint) plistBytes.Length);

        await SendAsync(lengthBytes, cancellationToken).ConfigureAwait(false);
        await SendAsync(plistBytes, cancellationToken).ConfigureAwait(false);
    }

    public PropertyNode? SendReceivePlist(PropertyNode data) {
        SendPlist(data);
        return ReceivePlist();
    }

    public async Task<PropertyNode?> SendReceivePlistAsync(PropertyNode data, CancellationToken cancellationToken) {
        await SendPlistAsync(data, cancellationToken: cancellationToken).ConfigureAwait(false);
        return await ReceivePlistAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Set a value in milliseconds, that determines how long the service connection will attempt to read/write for before timing out
    /// </summary>
    /// <param name="timeout">A value in milliseconds that detemines how long the service connection will wait before timing out</param>
    public void SetTimeout(int timeout = Timeout.Infinite) {
        // Update the internal timeout
        _timeout = timeout;

        // Update the currently active stream to use these timeouts.
        Stream.ReadTimeout = timeout;
        Stream.WriteTimeout = timeout;
    }

    public bool StartSsl(X509Certificate2 certificate) {
        if (_sslStream != null) {
            throw new InvalidOperationException("SSL stream already exists");
        }
        if (_networkStream == null) {
            throw new InvalidOperationException("Network stream is null");
        }
        _networkStream.Flush();

        _sslStream = new SslStream(_networkStream, true, UserCertificateValidationCallback, null, EncryptionPolicy.RequireEncryption) {
            ReadTimeout = _timeout,
            WriteTimeout = _timeout
        };
        try {
            // TLS v1.2 is supported since iOS 5 so we should specify this as a minimum
            _sslStream.AuthenticateAsClient(string.Empty, [certificate], SslProtocols.Tls12 | SslProtocols.Tls13, false);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "SSL authentication failed");
            return false;
        }

        return true;
    }

    public async Task<bool> StartSslAsync(X509Certificate2 certificate) {
        if (_sslStream != null) {
            throw new InvalidOperationException("SSL stream already exists");
        }
        if (_networkStream == null) {
            throw new InvalidOperationException("Network stream is null");
        }
        await _networkStream.FlushAsync().ConfigureAwait(false);

        _sslStream = new SslStream(_networkStream, true, UserCertificateValidationCallback, null, EncryptionPolicy.RequireEncryption) {
            ReadTimeout = _timeout,
            WriteTimeout = _timeout
        };
        try {
            // TLS v1.2 is supported since iOS 5 so we should specify this as a minimum
            await _sslStream.AuthenticateAsClientAsync(string.Empty, [certificate], SslProtocols.Tls12 | SslProtocols.Tls13, false);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "SSL authentication failed");
            return false;
        }

        return true;
    }
}

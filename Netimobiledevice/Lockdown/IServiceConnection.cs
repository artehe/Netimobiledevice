using Netimobiledevice.Plist;
using Netimobiledevice.Usbmuxd;
using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Lockdown;

/// <summary>
/// A wrapper for usbmux tcp-relay connections
/// </summary>
public interface IServiceConnection : IAsyncDisposable, IDisposable {
    // TODO remove IDisposable as we only want it to be async...
    // TODO Look into whether we keep this...
    bool IsConnected { get; }
    // TODO Consider whether we want to keep this or not...
    UsbmuxdDevice? MuxDevice { get; }
    // TODO Consider whether we want to keep this or not...
    Stream Stream { get; }

    // TODO remove this we only want the async version available....
    void Close();
    // TODO remove this we only want the async version available....
    byte[] Receive(int length = 4096);
    // TODO remove this we only want the async version available....
    PropertyNode? ReceivePlist();
    // TODO remove this we only want the async version available....
    byte[] ReceivePrefixed();
    // TODO remove this we only want the async version available....
    void SendPlist(PropertyNode data, PlistFormat format = PlistFormat.Xml);
    // TODO remove this we only want the async version available....
    PropertyNode? SendReceivePlist(PropertyNode data);
    // TODO look at the implementation of this....
    void SetTimeout(int timeout = Timeout.Infinite);
    // TODO remove this we only want the async version available....
    bool StartSsl(X509Certificate2 certificate);
    // TODO look at the implementation of this....
    Task<bool> StartSslAsync(X509Certificate2 certificate);

    /// <summary>Receive exactly <paramref name="size"/> bytes.</summary>
    ValueTask<byte[]> ReceiveAsync(int size, CancellationToken cancellationToken = default);
    ValueTask<PropertyNode?> ReceivePlistAsync(CancellationToken cancellationToken = default);
    ValueTask SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default);
    ValueTask SendPlistAsync(PropertyNode plist, PlistFormat plistFormat = PlistFormat.Xml, CancellationToken cancellationToken = default);
    ValueTask<PropertyNode?> SendReceivePlistAsync(PropertyNode data, PlistFormat plistFormat = PlistFormat.Xml, CancellationToken cancellationToken = default);
}

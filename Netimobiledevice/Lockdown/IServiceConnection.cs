using Netimobiledevice.Plist;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Lockdown;

/// <summary>
/// A wrapper for usbmux tcp-relay connections
/// </summary>
public interface IServiceConnection : IAsyncDisposable {
    /// <summary>Receive exactly <paramref name="size"/> bytes.</summary>
    ValueTask<byte[]> ReceiveAsync(int size, CancellationToken cancellationToken = default);
    ValueTask<PropertyNode?> ReceivePlistAsync(CancellationToken cancellationToken = default);
    ValueTask SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default);
    ValueTask SendPlistAsync(PropertyNode plist, PlistFormat plistFormat = PlistFormat.Xml, CancellationToken cancellationToken = default);
    ValueTask<PropertyNode?> SendReceivePlistAsync(PropertyNode data, PlistFormat plistFormat = PlistFormat.Xml, CancellationToken cancellationToken = default);
}

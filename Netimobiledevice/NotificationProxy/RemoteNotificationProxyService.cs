using Microsoft.Extensions.Logging;
using Netimobiledevice.Remoted;
using Netimobiledevice.Remoted.Xpc;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.NotificationProxy;

/// <summary>
/// Post and observe Darwin notifications over the native RemoteXPC notification proxy.
/// </summary>
/// <remarks>
/// This is the RSD-native counterpart of <see cref="NotificationProxyService"/>: instead of
/// tunnelling the classic lockdown service through its <c>.shim.remote</c> alias, it talks to the
/// notification proxy daemon directly over RemoteXPC. The message vocabulary is identical
/// (<c>PostNotification</c> / <c>ObserveNotification</c> / <c>RelayNotification</c>), only the
/// transport differs. Requires an iOS 17+ RSD tunnel. Use with <c>await using</c>.
/// </remarks>
/// <param name="rsd">RSD provider used to open the RemoteXPC service.</param>
/// <param name="insecure">When true, use the insecure relay meant for untrusted clients.</param>
public sealed class RemoteNotificationProxyService(RemoteServiceDiscoveryService rsd, bool insecure = false) : RemoteService(rsd, insecure ? InsecureServiceName : ServiceName) {
    public const string InsecureServiceName = "com.apple.mobile.insecure_notification_proxy.remote";
    public const string ServiceName = "com.apple.mobile.notification_proxy.remote";

    /// <summary>Post a notification on the device.</summary>
    /// <param name="name">Notification name to post (e.g. a Darwin notification name).</param>
    public Task NotifyPostAsync(string name, CancellationToken cancellationToken = default) => Service.SendRequestAsync(
        new XpcDictionary() {
            ["Command"] = new XpcString("PostNotification"),
            ["Name"] = new XpcString(name)
        },
        cancellationToken
    );

    /// <summary>
    /// Register interest in a notification so the device relays it back. Once registered, the
    /// device sends a <c>RelayNotification</c> message whenever the named notification fires,
    /// which can be read via <see cref="ReceiveNotificationAsync"/>.
    /// </summary>
    /// <param name="name">Notification name to observe.</param>
    public Task NotifyRegisterDispatchAsync(string name, CancellationToken cancellationToken = default) {
        Logger.LogDebug("Observing {Name}", name);
        return Service.SendRequestAsync(
            new XpcDictionary() {
                ["Command"] = new XpcString("ObserveNotification"),
                ["Name"] = new XpcString(name)
            },
            cancellationToken
        );
    }

    /// <summary>
    /// Yield notifications relayed from the device for previously observed names. Each yielded
    /// message has the form <c>{ "Command": "RelayNotification", "Name": &lt;notification name&gt; }</c>.
    /// </summary>
    public async IAsyncEnumerable<XpcDictionary> ReceiveNotificationAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    ) {
        while (!cancellationToken.IsCancellationRequested) {
            yield return await Service.ReceiveResponseAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}

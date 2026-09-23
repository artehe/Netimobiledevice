using Microsoft.Extensions.Logging;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Plist;
using Netimobiledevice.Remoted;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.NotificationProxy;

/// <summary>
/// Post and observe Darwin notifications on the device via the notification proxy lockdown service.
/// </summary>
/// <remarks>
/// Allows sending notifications to the device, registering interest in notifications so the device
/// relays them back, and iterating over the relayed notifications. A secure or insecure variant of
/// the service is selected by the <c>insecure</c> flag, and the RSD/tunnel variant is chosen
/// automatically for <see cref="RemoteServiceDiscoveryService"/> providers. This is a lockdown
/// service and is used with <c>await using</c>.
/// </remarks>
public sealed class NotificationProxyService : LockdownService {
    public const string InsecureServiceName = "com.apple.mobile.insecure_notification_proxy";
    public const string LockdownServiceName = "com.apple.mobile.notification_proxy";
    public const string RsdInsecureServiceName = "com.apple.mobile.insecure_notification_proxy.shim.remote";
    public const string RsdServiceName = "com.apple.mobile.notification_proxy.shim.remote";

    private readonly TimeSpan? _timeout;

    /// <param name="lockdown">Service provider used to start the service and reach the device.</param>
    /// <param name="insecure">When true, use the insecure notification proxy service instead of the secure one.</param>
    /// <param name="timeout">Optional receive timeout applied to each read from the service connection.</param>
    public NotificationProxyService(
        LockdownServiceProvider lockdown,
        bool insecure = false,
        TimeSpan? timeout = null,
        ILogger? logger = null
    ) : base(lockdown, ResolveServiceName(lockdown, insecure), logger: logger) {
        if (timeout is { } t) {
            // Applies to synchronous reads; async reads are bounded in ReceiveNotificationAsync.
            Service.SetTimeout((int) t.TotalMilliseconds);
            _timeout = t;
        }
    }

    private static string ResolveServiceName(
        LockdownServiceProvider lockdown,
        bool insecure
    ) => (lockdown is RemoteServiceDiscoveryService, insecure) switch {
        (true, true) => RsdInsecureServiceName,
        (true, false) => RsdServiceName,
        (false, true) => InsecureServiceName,
        (false, false) => LockdownServiceName,
    };

    /// <summary>
    /// Register interest in a notification so the device relays it back. Once registered, the
    /// device sends a message whenever the named notification fires, which can be read via
    /// <see cref="ReceiveNotificationAsync"/>.
    /// </summary>
    /// <param name="name">Notification name to observe.</param>
    public Task NotifyRegisterDispatchAsync(
        string name,
        CancellationToken cancellationToken = default
    ) {
        Logger.LogDebug("Observing {Name}", name);
        return Service.SendPlistAsync(
            new DictionaryNode {
                ["Command"] = new StringNode("ObserveNotification"),
                ["Name"] = new StringNode(name)
            },
            PlistFormat.Xml,
            cancellationToken
        );
    }

    /// <summary>
    /// Post a notification on the device. Sends a <c>PostNotification</c> command, causing the
    /// device to broadcast the named notification.
    /// </summary>
    /// <param name="name">Notification name to post (e.g. a Darwin notification name).</param>
    public Task NotifyPostAsync(
        string name,
        CancellationToken cancellationToken = default
    ) => Service.SendPlistAsync(
        new DictionaryNode {
            ["Command"] = new StringNode("PostNotification"),
            ["Name"] = new StringNode(name)
        },
        PlistFormat.Xml,
        cancellationToken
    );

    /// <summary>
    /// Yield notifications relayed from the device for previously observed names. Continuously
    /// reads from the service and yields each received message until the connection is closed.
    /// </summary>
    /// <exception cref="NotificationTimeoutException">
    /// No notification arrived within the configured timeout.
    /// </exception>
    public async IAsyncEnumerable<DictionaryNode> ReceiveNotificationAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    ) {
        while (true) {
            DictionaryNode message;
            using (CancellationTokenSource readCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)) {
                if (_timeout is { } t) {
                    readCts.CancelAfter(t);
                }

                try {
                    PropertyNode? propertyNode = await Service.ReceivePlistAsync(readCts.Token).ConfigureAwait(false);
                    message = propertyNode?.AsDictionaryNode() ?? [];
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
                    throw new NotificationProxyTimeoutException();
                }
                catch (SocketException e) when (e.SocketErrorCode == SocketError.TimedOut) {
                    throw new NotificationProxyTimeoutException();
                }

                yield return message;
            }
        }
    }
}

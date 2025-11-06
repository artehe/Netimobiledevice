using Microsoft.Extensions.Logging;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Plist;
using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.NotificationProxy;

/// <summary>
/// Send and receive notifications from the device for example informing a backup sync is about to occur.
/// </summary>
public sealed class NotificationProxyService(
    LockdownServiceProvider lockdown,
    bool useInsecureService = false,
    ILogger? logger = null
) : LockdownService(lockdown, ServiceNameUsed, GetNotificationProxyServiceConnection(lockdown, useInsecureService), logger: logger) {
    private const string LOCKDOWN_SERVICE_NAME = "com.apple.mobile.notification_proxy";
    private const string RSD_SERVICE_NAME = "com.apple.mobile.notification_proxy.shim.remote";

    private const string INSECURE_LOCKDOWN_SERVICE_NAME = "com.apple.mobile.insecure_notification_proxy";
    private const string RSD_INSECURE_SERVICE_NAME = "com.apple.mobile.insecure_notification_proxy.shim.remote";

    private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
    private Task? _notificationListenerTask;

    private static string ServiceNameUsed { get; set; } = LOCKDOWN_SERVICE_NAME;

    public event EventHandler<ReceivedNotificationEventArgs>? ReceivedNotification;

    private static ServiceConnection? GetNotificationProxyServiceConnection(LockdownServiceProvider lockdown, bool useInsecureService) {
        if (lockdown is LockdownClient) {
            if (useInsecureService) {
                ServiceNameUsed = INSECURE_LOCKDOWN_SERVICE_NAME;
            }
            else {
                ServiceNameUsed = LOCKDOWN_SERVICE_NAME;
            }
        }
        else {
            if (useInsecureService) {
                ServiceNameUsed = RSD_INSECURE_SERVICE_NAME;
            }
            else {
                ServiceNameUsed = RSD_SERVICE_NAME;
            }
        }
        return lockdown.StartLockdownService(ServiceNameUsed, useTrustedConnection: !useInsecureService);
    }

    public override void Dispose() {
        Stop();
        base.Dispose();
    }

    private async Task<string?> GetNotificationAsync(CancellationToken cancellationToken) {
        try {
            PropertyNode? plist = await Service.ReceivePlistAsync(cancellationToken).ConfigureAwait(false);
            if (plist != null) {
                DictionaryNode dict = plist.AsDictionaryNode();
                if (dict.TryGetValue("Command", out PropertyNode? commandNode)) {
                    if (commandNode.AsStringNode().Value == "RelayNotification") {
                        if (dict.TryGetValue("Name", out PropertyNode? notificationNameNode)) {
                            string notificationName = notificationNameNode.AsStringNode().Value;
                            Logger.LogDebug("Got notification {notificationName}", notificationName);
                            return notificationName;
                        }
                    }
                    else if (commandNode.AsStringNode().Value == "ProxyDeath") {
                        Logger.LogError("NotificationProxy died");
                        throw new NotificationProxyException("Notification proxy died, can't listen to notifications anymore");
                    }
                    else {
                        Logger.LogWarning("Unknown NotificationProxy command {command}", commandNode.AsStringNode().Value);
                    }
                }
            }
        }
        catch (ArgumentException ex) {
            Logger.LogError(ex, "Error");
        }
        return null;
    }

    private async Task NotificationListener() {
        Service.SetTimeout(5000);
        do {
            try {
                string? notification = await GetNotificationAsync(_cancellationTokenSource.Token).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(notification)) {
                    ReceivedNotification?.Invoke(this, new ReceivedNotificationEventArgs(notification, this.Lockdown.Udid));
                }
            }
            catch (IOException ex) {
                Logger.LogDebug(ex, "Recieved IO exception");
            }
            catch (ObjectDisposedException) {
                // If the object is disposed the most likely reason is that the service is closed
                break;
            }
            catch (TimeoutException) {
                Logger.LogDebug("No notifications received yet, trying again");
            }
            catch (Exception ex) {
                if (!_cancellationTokenSource.Token.IsCancellationRequested) {
                    Logger.LogError(ex, "Notification proxy listener has an error");
                    throw;
                }
            }
            await Task.Delay(200, _cancellationTokenSource.Token).ConfigureAwait(false);
        } while (!_cancellationTokenSource.Token.IsCancellationRequested);
    }

    /// <summary>
    /// Posts the specified notification.
    /// </summary>
    /// <param name="notification">The notification to post.</param>
    public void Post(string notification) {
        DictionaryNode msg = new DictionaryNode() {
            { "Command", new StringNode("PostNotification") },
            { "Name", new StringNode(notification) }
        };
        Service.SendPlist(msg);
    }

    /// <summary>
    /// Posts the specified notification.
    /// </summary>
    /// <param name="notification">The notification to post.</param>
    public async Task PostAsync(string notification) {
        DictionaryNode msg = new DictionaryNode() {
            { "Command", new StringNode("PostNotification") },
            { "Name", new StringNode(notification) }
        };
        await Service.SendPlistAsync(msg).ConfigureAwait(false);
    }

    /// <summary>
    /// Attempts to observe all builtin receivable notifications.
    /// </summary>
    /// <returns></returns>
    public void ObserveAll() {
        foreach (PropertyInfo prop in typeof(ReceivableNotification).GetProperties()) {
            string notification = prop.GetValue(typeof(ReceivableNotification), null)?.ToString() ?? string.Empty;
            if (string.IsNullOrEmpty(notification)) {
                continue;
            }
            ObserveNotification(notification);
        }
    }

    /// <summary>
    /// Attempts to observe all builtin receivable notifications.
    /// </summary>
    /// <returns></returns>
    public async Task ObserveAllAsync() {
        foreach (PropertyInfo prop in typeof(ReceivableNotification).GetProperties()) {
            string notification = prop.GetValue(typeof(ReceivableNotification), null)?.ToString() ?? string.Empty;
            if (string.IsNullOrEmpty(notification)) {
                continue;
            }
            await ObserveNotificationAsync(notification).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Inform the device of the notification we want to observe.
    /// </summary>
    /// <param name="notification"></param>
    public void ObserveNotification(string notification) {
        DictionaryNode request = new DictionaryNode() {
            { "Command", new StringNode("ObserveNotification") },
            { "Name", new StringNode(notification) }
        };
        Service.SendPlist(request);
    }

    /// <summary>
    /// Inform the device of the notification we want to observe.
    /// </summary>
    /// <param name="notification"></param>
    public async Task ObserveNotificationAsync(string notification) {
        DictionaryNode request = new DictionaryNode() {
            { "Command", new StringNode("ObserveNotification") },
            { "Name", new StringNode(notification) }
        };
        await Service.SendPlistAsync(request).ConfigureAwait(false);
    }

    /// <summary>
    /// Starts observing any notifications from the device if it is not doing so already
    /// </summary>
    public void Start() {
        if (_notificationListenerTask == null) {
            _cancellationTokenSource = new CancellationTokenSource();
            _notificationListenerTask = Task.Run(NotificationListener, _cancellationTokenSource.Token);
        }
    }

    /// <summary>
    /// Stops observing any notifications from the device
    /// </summary>
    public void Stop() {
        _cancellationTokenSource.Cancel();
        _notificationListenerTask = null;
    }
}

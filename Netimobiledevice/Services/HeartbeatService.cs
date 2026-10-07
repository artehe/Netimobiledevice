using Microsoft.Extensions.Logging;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Plist;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Services;

/// <summary>
/// Keeps an active connection alive with the device's heartbeat lockdown service, by periodically 
/// sending <c>Marco</c> messages and expecting a <c>Polo</c> reply
/// </summary>
public sealed class HeartbeatService(
    LockdownServiceProvider lockdown,
    ILogger? logger
) : LockdownService(
    lockdown,
    lockdown is LockdownClient ? LockdownServiceName : RemoteServiceName,
    logger: logger
) {
    private const string LockdownServiceName = "com.apple.mobile.heartbeat";
    private const string RemoteServiceName = "com.apple.mobile.heartbeat.shim.remote";

    /// <summary>
    /// Starts the heartbeat exchange loop.
    /// </summary>
    /// <param name="interval">
    /// When set, the loop stops once roughly this much time has elapsed since it started;
    /// when <see langword="null"/>, the loop runs until <paramref name="cancellationToken"/> is cancelled.
    /// </param>
    /// <param name="cancellationToken">Cancels the loop.</param>
    public async Task StartAsync(TimeSpan? interval = null, CancellationToken cancellationToken = default) {
        long start = Stopwatch.GetTimestamp();

        await Lockdown.StartLockdownServiceAsync(ServiceName, cancellationToken: cancellationToken);
        if (Service is not null) {
            using (Service) {
                while (true) {
                    PropertyNode? response = await Service.ReceivePlistAsync(cancellationToken);
                    DictionaryNode responseDict = response?.AsDictionaryNode() ?? [];
                    if (Logger.IsEnabled(LogLevel.Debug)) {
                        Logger.LogDebug("{Response}", PropertyList.SaveAsStringAsync(responseDict, PlistFormat.Xml));
                    }

                    if (interval is TimeSpan limit && Stopwatch.GetElapsedTime(start) >= limit) {
                        break;
                    }

                    DictionaryNode message = new DictionaryNode() {
                        ["Command"] = new StringNode("Polo")
                    };
                    await Service.SendPlistAsync(message, PlistFormat.Xml, cancellationToken);
                }
            }
        }
    }
}

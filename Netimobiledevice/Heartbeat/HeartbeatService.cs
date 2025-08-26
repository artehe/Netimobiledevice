using Microsoft.Extensions.Logging;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Plist;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Heartbeat;

/// <summary>
/// Used to keep an active connection with lockdownd by providing a regular ping
/// </summary>
public sealed class HeartbeatService(LockdownServiceProvider lockdown, ILogger? logger) : LockdownService(lockdown, LOCKDOWN_SERVICE_NAME, RSD_SERVICE_NAME, logger: logger)
{
    private const string LOCKDOWN_SERVICE_NAME = "com.apple.mobile.heartbeat";
    private const string RSD_SERVICE_NAME = "com.apple.mobile.heartbeat.shim.remote";

    private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
    /// <summary>
    /// Have the interval be 10 seconds as default for the heartbeat
    /// </summary>
    private int _interval = 10 * 1000;
    private Task? _heartbeatTask;

    private async Task Heartbeat()
    {
        Service.SetTimeout(500);
        do {
            try {
                PropertyNode? response = await Service.ReceivePlistAsync(_cancellationTokenSource.Token).ConfigureAwait(false);
                DictionaryNode responseDict = response?.AsDictionaryNode() ?? [];

                // If the interval exists update it adding an extra second to be certain we have waited long enough
                if (responseDict.TryGetValue("Interval", out PropertyNode? intervalNode)) {
                    _interval = (int) ((intervalNode.AsIntegerNode().Value + 1) * 1000);
                }

                await Service.SendPlistAsync(
                    new DictionaryNode() {
                        { "Command", new StringNode("Polo") }
                    }
                ).ConfigureAwait(false);
            }
            catch (IOException) {
                // If there is an IO exception we also have to assume that the service is closed so we abort the listener
                break;
            }
            catch (ObjectDisposedException) {
                // If the object is disposed the most likely reason is that the service is closed
                break;
            }
            catch (TimeoutException) {
                Logger.LogDebug("No heartbeat received, trying again");
            }
            catch (Exception ex) {
                if (!_cancellationTokenSource.Token.IsCancellationRequested) {
                    throw new HeartbeatException("Heartbeat service has an error", ex);
                }
            }
            await Task.Delay(_interval);
        } while (!_cancellationTokenSource.Token.IsCancellationRequested);
    }

    public override void Dispose()
    {
        Stop();
        base.Dispose();
    }

    /// <summary>
    /// Start the heartbeat service
    /// </summary>
    public void Start()
    {
        if (_heartbeatTask == null) {
            _cancellationTokenSource = new CancellationTokenSource();
            _heartbeatTask = Task.Run(Heartbeat, _cancellationTokenSource.Token);
        }
    }

    public void Stop()
    {
        _cancellationTokenSource.Cancel();
        _heartbeatTask = null;
    }
}

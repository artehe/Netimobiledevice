using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Netimobiledevice.Bonjour;
using Netimobiledevice.Plist;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Lockdown;

/// <summary>
/// Base class for all services that wrap a single lockdown service on the device.
/// </summary>
public abstract class LockdownService : IAsyncDisposable {
    private readonly Lock _lock = new();

    /// <summary>
    /// Shared in-flight connection attempt: the first ConnectAsync() call creates it; concurrent callers
    /// join it instead of racing to call StartLockdownServiceAsync() multiple times.
    /// </summary>
    private TaskCompletionSource? _connectTcs;
    private readonly bool _includeEscrowBag;
    private ServiceConnection? _service;

    /// <summary>Service provider used to start the service and reach the device.</summary>
    protected LockdownServiceProvider Lockdown { get; }
    /// <summary>Logger for this service.</summary>
    protected ILogger Logger { get; }
    /// <summary>
    /// The established service connection, or <see langword="null"/> if not connected yet.
    /// Use <see cref="GetServiceAsync"/> to connect on demand.
    /// </summary>
    public ServiceConnection? Service {
        get {
            lock (_lock) {
                return _service;
            }
        }
    }
    /// <summary>Name of the wrapped lockdown service.</summary>
    public string ServiceName { get; }

    /// <summary>
    /// Create a new LockdownService instance
    /// </summary>
    /// <param name="lockdown">Service provider used to start the service and communicate with the device.</param>
    /// <param name="serviceName">Name of the lockdown service to wrap; started lazily on first connection.</param>
    /// <param name="service">
    /// An already-established service connection. When provided, no connection is started; otherwise a
    /// connection to <paramref name="serviceName"/> is opened lazily.
    /// </param>
    /// <param name="includeEscrowBag">When true, include the host escrow bag when starting the service.</param>
    /// <param name="logger">Optional logger; defaults to a no-op logger.</param>
    protected LockdownService(
        LockdownServiceProvider lockdown,
        string serviceName,
        ServiceConnection? service = null,
        bool includeEscrowBag = false,
        ILogger? logger = null
    ) {
        ArgumentNullException.ThrowIfNull(lockdown);
        ArgumentException.ThrowIfNullOrEmpty(serviceName);

        Lockdown = lockdown;
        ServiceName = serviceName;
        _includeEscrowBag = includeEscrowBag;
        _service = service;
        Logger = logger ?? NullLogger.Instance;
    }

    /// <summary>
    /// Allow retry after a failed or cancelled attempt (only if it's still the current one).
    /// </summary>
    /// <param name="attempt"></param>
    private void ResetConnectAttempt(TaskCompletionSource attempt) {
        lock (_lock) {
            if (ReferenceEquals(_connectTcs, attempt)) {
                _connectTcs = null;
            }
        }
    }

    /// <summary>Close the underlying service connection (if any) and reset connection state.</summary>
    public async Task CloseAsync() {
        ServiceConnection? service;

        lock (_lock) {
            service = _service;
            _service = null;
            _connectTcs = null;
        }

        if (service is not null) {
            await service.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Start and connect the wrapped service if not already connected.
    /// </summary>
    /// <remarks>
    /// Does nothing when a connection already exists. Concurrent callers share a single in-flight connection
    /// attempt rather than starting the service multiple times; on failure the attempt is reset so it may be
    /// retried. If the caller that owns the attempt is cancelled, callers joined to it observe the
    /// cancellation as well.
    /// </remarks>
    /// <exception cref="StartServiceException">The service failed to start.</exception>
    public async Task ConnectAsync(CancellationToken cancellationToken = default) {
        bool shouldJoin = false;

        TaskCompletionSource tcs;
        lock (_lock) {
            if (_service is not null) {
                return;
            }

            if (_connectTcs is TaskCompletionSource inFlight) {
                // Another caller owns the attempt: join it (our own token only cancels our wait).
                tcs = inFlight;
                shouldJoin = true;
            }
            else {
                tcs = _connectTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        if (!shouldJoin) {
            // We own the connection attempt.
            try {
                ServiceConnection connection = await Lockdown.StartLockdownServiceAsync(ServiceName, _includeEscrowBag, cancellationToken: cancellationToken).ConfigureAwait(false);
                lock (_lock) {
                    _service = connection;
                    _connectTcs = null;
                }

                tcs.SetResult();
                return;
            }
            catch (OperationCanceledException) {
                ResetConnectAttempt(tcs);
                tcs.TrySetCanceled(cancellationToken);
                throw;
            }
            catch (Exception ex) {
                ResetConnectAttempt(tcs);
                tcs.TrySetException(ex);
                throw;
            }
        }

        await tcs.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async ValueTask DisposeAsync() {
        await CloseAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    public static async IAsyncEnumerable<(string, TcpLockdownClient)> GetMobdev2Lockdowns(
        string? udid = null,
        string? pairRecordsPath = null,
        bool onlyPaired = false
    ) {
        Dictionary<string, DictionaryNode> records = [];
        DirectoryInfo pairRecordsDirectory = new DirectoryInfo(pairRecordsPath ?? "");
        foreach (FileInfo file in pairRecordsDirectory.GetFiles("*.plist")) {
            if (file.Name.StartsWith("remote_", StringComparison.InvariantCulture)) {
                // Skip RemotePairing records
                continue;
            }

            string recordUdid = file.Name.Replace(".plist", "");
            if (udid != null && recordUdid != udid) {
                continue;
            }

            DictionaryNode record = PropertyList.LoadFromByteArray(File.ReadAllBytes(file.FullName)).AsDictionaryNode();
            string wiFiMACAddress = record["WiFiMACAddress"].AsStringNode().Value;
            records.Add(wiFiMACAddress, record);
        }

        MdnsBrowser mdnsBrowser = new MdnsBrowser(new MdnsSocketFactory(), new MdnsInterfaceResolver());
        foreach (ServiceInstance answer in await mdnsBrowser.BrowseMobdev2Async(TimeSpan.FromSeconds(2)).ConfigureAwait(false)) {
            if (!answer.Instance.Contains('@')) {
                continue;
            }
            string wifiMacAddress = answer.Instance.Split('@', 1)[0];
            DictionaryNode record = records[wifiMacAddress];

            if (onlyPaired && record == null) {
                continue;
            }

            foreach (Address address in answer.Addresses) {
                TcpLockdownClient lockdown;
                try {
                    lockdown = await MobileDevice.CreateUsingTcpAsync(hostname: address.Ip, autopair: false, pairRecord: record);
                }
                catch (Exception) {
                    continue;
                }

                if (onlyPaired && !lockdown.IsPaired) {
                    lockdown.Close();
                    continue;
                }
                yield return (address.Ip, lockdown);
            }
        }
    }

    /// <summary>
    /// Returns the service connection, connecting first if necessary.
    /// This replaces the lazy-proxy behavior of the Python <c>service</c> property.
    /// </summary>
    /// <exception cref="StartServiceException">The service failed to start.</exception>
    public async ValueTask<ServiceConnection> GetServiceAsync(CancellationToken cancellationToken = default) {
        await ConnectAsync(cancellationToken).ConfigureAwait(false);
        return Service ?? throw new InvalidOperationException("service is not connected");
    }
}

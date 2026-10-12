using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Plist;

namespace NetimobiledeviceTest.TestProviders;

internal sealed class TestLockdownProvider(ServiceConnection connection) : LockdownServiceProvider {
    private readonly ServiceConnection _connection = connection;

    public override ILogger Logger => NullLogger.Instance;

    public override Version OsVersion => new(18, 0);

    public override PropertyNode? GetValue(string? domain, string? key) => null;

    public override Task<PropertyNode?> GetValueAsync(
        string? domain,
        string? key
    ) => Task.FromResult<PropertyNode?>(null);

    public override ServiceConnection StartLockdownService(
        string name,
        bool useEscrowBag = false,
        bool useTrustedConnection = true
    ) => _connection;

    public override Task<ServiceConnection> StartLockdownServiceAsync(
        string name,
        bool useEscrowBag = false,
        bool useTrustedConnection = true,
        CancellationToken cancellationToken = default
    ) => Task.FromResult(_connection);
}

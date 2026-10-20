using Netimobiledevice.NotificationProxy;
using Netimobiledevice.Services.Afc;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Services.Mobilebackup2;

internal sealed class BackupLock : IAsyncDisposable {
    private const string SyncLockFilePath = "/com.apple.itunes.lock_sync";

    private readonly AfcService _afc;
    private readonly ulong? _lockFileHandle;
    private readonly NotificationProxyService _notificationProxy;

    private BackupLock(
        AfcService afc,
        NotificationProxyService notificationProxy,
        ulong? lockfileHandle
    ) {
        _afc = afc;
        _notificationProxy = notificationProxy;
        _lockFileHandle = lockfileHandle;
    }

    internal static async Task<BackupLock> Create(
        AfcService afc,
        NotificationProxyService notificationProxy,
        CancellationToken ct
    ) {
        await notificationProxy.NotifyPostAsync(SendableNotificaton.SyncWillStart, ct).ConfigureAwait(false);

        ulong lockfile = await afc.FileOpenAsync(SyncLockFilePath, AfcFileOpenMode.ReadWrite, ct).ConfigureAwait(false);
        if (lockfile != 0) {
            await notificationProxy.NotifyPostAsync(SendableNotificaton.SyncLockRequest, ct).ConfigureAwait(false);

            bool locked = false;
            for (int attempt = 0; attempt < 50; attempt++) {
                try {
                    await afc.LockAsync(lockfile, AfcLockModes.ExclusiveLock, ct).ConfigureAwait(false);
                }
                catch (AfcException e) when (e.Status == AfcError.OpWouldBlock) {
                    await Task.Delay(TimeSpan.FromMilliseconds(200), ct).ConfigureAwait(false);
                    continue;
                }
                catch (AfcException) {
                    await afc.FileCloseAsync(lockfile, ct).ConfigureAwait(false);
                    throw;
                }

                await notificationProxy.NotifyPostAsync(SendableNotificaton.SyncDidStart, ct).ConfigureAwait(false);
                locked = true;
                break;
            }

            if (!locked) {
                await afc.FileCloseAsync(lockfile, ct).ConfigureAwait(false);
                throw new AfcException("Failed to lock itunes sync file");
            }
        }
        return new BackupLock(afc, notificationProxy, lockfile);
    }

    public async ValueTask DisposeAsync() {
        if (_lockFileHandle is not null) {
            await _afc.LockAsync(_lockFileHandle.Value, AfcLockModes.Unlock).ConfigureAwait(false);
            await _afc.FileCloseAsync(_lockFileHandle.Value).ConfigureAwait(false);
        }
        await _notificationProxy.NotifyPostAsync(SendableNotificaton.SyncDidFinish).ConfigureAwait(false);
    }
}

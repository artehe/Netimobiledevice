using Netimobiledevice.NotificationProxy;
using Netimobiledevice.Services;
using Netimobiledevice.Services.Afc;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Backup;

internal sealed class BackupLock(AfcService afc, NotificationProxyService np) : IDisposable {
    private const string SYNC_LOCK_FILE_PATH = "/com.apple.itunes.lock_sync";

    private readonly AfcService _afc = afc;
    private readonly NotificationProxyService _np = np;

    private ulong _syncLockFileHandle;

    public void Dispose() {
        Task.Run(async () => {
            await _afc.LockAsync(_syncLockFileHandle, AfcLockModes.Unlock, CancellationToken.None).ConfigureAwait(false);
            await _afc.FileCloseAsync(_syncLockFileHandle, CancellationToken.None).ConfigureAwait(false);
        }).GetAwaiter().GetResult();
        _np.NotifyPostAsync(SendableNotificaton.SyncDidFinish);
    }

    public async Task AquireBackupLock(CancellationToken cancellationToken) {
        await _np.NotifyPostAsync(SendableNotificaton.SyncWillStart, cancellationToken).ConfigureAwait(false);
        _syncLockFileHandle = await _afc.FileOpenAsync(SYNC_LOCK_FILE_PATH, AfcFileOpenMode.ReadWrite, cancellationToken).ConfigureAwait(false);
        if (_syncLockFileHandle > 0) {
            await _np.NotifyPostAsync(SendableNotificaton.SyncLockRequest, cancellationToken).ConfigureAwait(false);

            bool lockAquired = false;
            for (int i = 0; i < 50; i++) {
                try {
                    await _afc.LockAsync(_syncLockFileHandle, AfcLockModes.ExclusiveLock, cancellationToken).ConfigureAwait(false);
                    lockAquired = true;
                    break;
                }
                catch (AfcException e) {
                    if (e.Status == AfcError.OpWouldBlock) {
                        await Task.Delay(200, cancellationToken).ConfigureAwait(false);
                    }
                    else {
                        await _afc.FileCloseAsync(_syncLockFileHandle, cancellationToken).ConfigureAwait(false);
                        throw;
                    }
                }
                catch (Exception) {
                    throw;
                }
            }

            if (lockAquired) {
                await _np.NotifyPostAsync(SendableNotificaton.SyncDidStart, cancellationToken).ConfigureAwait(false);
            }
        }
        else {
            throw new AfcException("Failed to get file handle for iTunes backup sync file");
        }
    }
}

using Netimobiledevice.Afc;
using Netimobiledevice.NotificationProxy;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Backup;

internal sealed class BackupLock(AfcService afc, NotificationProxyService np) : IDisposable
{
    private const string SYNC_LOCK_FILE_PATH = "/com.apple.itunes.lock_sync";

    private readonly AfcService _afc = afc;
    private readonly NotificationProxyService _np = np;

    private ulong _syncLockFileHandle;

    public void Dispose()
    {
        Task.Run(async () => {
            await _afc.Lock(_syncLockFileHandle, AfcLockModes.Unlock, CancellationToken.None).ConfigureAwait(false);
            await _afc.FileClose(_syncLockFileHandle, CancellationToken.None).ConfigureAwait(false);
        }).GetAwaiter().GetResult();
        _np.Post(SendableNotificaton.SyncDidFinish);
    }

    public async Task AquireBackupLock(CancellationToken cancellationToken)
    {
        await _np.PostAsync(SendableNotificaton.SyncWillStart).ConfigureAwait(false);
        _syncLockFileHandle = await _afc.FileOpen(SYNC_LOCK_FILE_PATH, cancellationToken, AfcFileOpenMode.ReadWrite).ConfigureAwait(false);
        if (_syncLockFileHandle > 0) {
            await _np.PostAsync(SendableNotificaton.SyncLockRequest).ConfigureAwait(false);

            bool lockAquired = false;
            for (int i = 0; i < 50; i++) {
                try {
                    await _afc.Lock(_syncLockFileHandle, AfcLockModes.ExclusiveLock, cancellationToken).ConfigureAwait(false);
                    lockAquired = true;
                    break;
                }
                catch (AfcException e) {
                    if (e.AfcError == AfcError.OpWouldBlock) {
                        await Task.Delay(200, cancellationToken).ConfigureAwait(false);
                    }
                    else {
                        await _afc.FileClose(_syncLockFileHandle, cancellationToken).ConfigureAwait(false);
                        throw;
                    }
                }
                catch (Exception) {
                    throw;
                }
            }

            if (lockAquired) {
                await _np.PostAsync(SendableNotificaton.SyncDidStart).ConfigureAwait(false);
            }
        }
        else {
            throw new AfcException("Failed to get file handle for iTunes backup sync file");
        }
    }
}

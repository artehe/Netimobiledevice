using Netimobiledevice.Afc;
using Netimobiledevice.NotificationProxy;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Backup
{
    internal sealed class BackupLock : IDisposable
    {
        private const string SYNC_LOCK_FILE_PATH = "/com.apple.itunes.lock_sync";

        private readonly AfcService _afc;
        private readonly NotificationProxyService _np;

        private ulong _syncLockFileHandle;

        public BackupLock(AfcService afc, NotificationProxyService np)
        {
            _afc = afc;
            _np = np;
        }

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
            _np.Post(SendableNotificaton.SyncWillStart);
            _syncLockFileHandle = await _afc.FileOpen(SYNC_LOCK_FILE_PATH, cancellationToken, AfcFileOpenMode.ReadWrite).ConfigureAwait(false);
            if (_syncLockFileHandle > 0) {
                _np.Post(SendableNotificaton.SyncLockRequest);

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
                    _np.Post(SendableNotificaton.SyncDidStart);
                }
            }
            else {
                throw new AfcException("Failed to get file handle for iTunes backup sync file");
            }
        }
    }
}

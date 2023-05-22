using Netimobiledevice.Exceptions;
using System;
using System.Threading;

namespace Netimobiledevice.Lockdown.Services
{
    internal class DeviceBackupLock : IDisposable
    {
        private const string NP_SYNC_DID_FINISH = "com.apple.itunes-mobdev.syncDidFinish";
        private const string NP_SYNC_DID_START = "com.apple.itunes-mobdev.syncDidStart";
        private const string NP_SYNC_LOCK_REQUEST = "com.apple.itunes-mobdev.syncLockRequest";
        private const string NP_SYNC_WILL_START = "com.apple.itunes-mobdev.syncWillStart";

        private readonly AfcService _afcService;
        private readonly NotificationProxyService _notificationProxyService;

        private ulong lockfile = 0;

        public DeviceBackupLock(AfcService afcService, NotificationProxyService notificationProxyService)
        {
            _afcService = afcService;
            _notificationProxyService = notificationProxyService;
        }

        public void AquireLock()
        {
            _notificationProxyService.NotifyPost(NP_SYNC_WILL_START);
            lockfile = _afcService.FileOpen("/com.apple.itunes.lock_sync", "r+");

            if (lockfile != 0) {
                _notificationProxyService.NotifyPost(NP_SYNC_LOCK_REQUEST);
                for (int i = 0; i < 50; i++) {
                    bool lockAquired = false;
                    try {
                        _afcService.Lock(lockfile, AfcLockModes.ExclusiveLock);
                        lockAquired = true;
                    }
                    catch (AfcException e) {
                        if (e.AfcError == AfcError.OpWouldBlock) {
                            Thread.Sleep(200);
                        }
                        else {
                            _afcService.FileClose(lockfile);
                            throw;
                        }
                    }
                    catch (Exception) {
                        throw;
                    }

                    if (lockAquired) {
                        _notificationProxyService.NotifyPost(NP_SYNC_DID_START);
                        break;
                    }
                }
            }
            else {
                // Lock failed
                _afcService.FileClose(lockfile);
                throw new Exception("Failed to lock iTunes sync file");
            }
        }

        public void Dispose()
        {
            _afcService.Lock(lockfile, AfcLockModes.Unlock);
            _afcService.FileClose(lockfile);
            _notificationProxyService.NotifyPost(NP_SYNC_DID_FINISH);
        }
    }
}

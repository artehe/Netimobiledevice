namespace Netimobiledevice.Services.Afc;

/// <summary>Operations for <see cref="AfcService.LockAsync"/>.</summary>
public enum AfcLockModes : ulong {
    SharedLock = 1 | 4,
    ExclusiveLock = 2 | 4,
    Unlock = 8 | 4
}

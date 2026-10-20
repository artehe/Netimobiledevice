namespace Netimobiledevice.Services.Mobilebackup2;

/// <summary>
/// Result codes for Service operations.
/// </summary>
public enum Mb2ResultCode : byte {
    Success = 0,
    LocalError = 6,
    RemoteError = 11,
    FileData = 12,
    InsufficientDiskSpace = 105,
    Skipped = 255
}

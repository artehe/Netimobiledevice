namespace Netimobiledevice.Backup
{
    /// <summary>
    /// Result codes for Mobilebackup2 Service operations.
    /// </summary>
    public enum ResultCode : byte
    {
        Success = 0x00,
        LocalError = 0x06,
        RemoteError = 0x0B,
        FileData = 0x0C,
        Skipped = 0xFF
    }
}

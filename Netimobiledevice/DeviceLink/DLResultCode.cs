namespace Netimobiledevice.DeviceLink
{
    /// <summary>
    /// Result codes for Service operations.
    /// </summary>
    public enum DLResultCode : byte
    {
        Success = 0x00,
        LocalError = 0x06,
        RemoteError = 0x0B,
        FileData = 0x0C,
        Skipped = 0xFF
    }
}

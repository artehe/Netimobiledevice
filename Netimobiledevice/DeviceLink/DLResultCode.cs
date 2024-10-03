namespace Netimobiledevice.DeviceLink
{
    /// <summary>
    /// Result codes for Service operations.
    /// </summary>
    public enum DLResultCode : byte
    {
        // Standard error codes
        Success = 0x00,
        LocalError = 0x06,
        RemoteError = 0x0B,
        FileData = 0x0C,
        Skipped = 0xFF,

        // Custom error codes
        BackupDeniedByOrganisation = 0x26,
        MessageComplete = 0xA0,
        MissingRequiredEncryptionPassword = 0xCF,
        DeviceLocked = 0xD0,
        UnexpectedError = 0xFD,
        UnknownMessage = 0xFE
    }
}

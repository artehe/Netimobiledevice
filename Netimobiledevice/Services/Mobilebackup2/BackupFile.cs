namespace Netimobiledevice.Services.Mobilebackup2;

/// <summary>
/// Identifies a single backup file
/// </summary>
public sealed record BackupFile(
    string? FileId = null,
    string? Domain = null,
    string? RelativePath = null,
    string? FileName = null,
    string? DeviceName = null
);

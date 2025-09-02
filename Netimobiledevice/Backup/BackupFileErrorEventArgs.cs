namespace Netimobiledevice.Backup;

/// <summary>
/// EventArgs for File Transfer Error events.
/// </summary>
public class BackupFileErrorEventArgs(BackupFile file, string details) : BackupFileEventArgs(file)
{
    /// <summary>
    /// Indicates whether the backup should be cancelled.
    /// </summary>
    public bool Cancel { get; set; }
    public string Details { get; set; } = details;
}

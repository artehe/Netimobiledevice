using System;

namespace Netimobiledevice.Backup;

/// <summary>
/// EventArgs for BackupFile related events.
/// </summary>
public class BackupFileEventArgs : EventArgs
{
    /// <summary>
    /// The BackupFile related to the event.
    /// </summary>
    public BackupFile File { get; }
    /// <summary>
    /// The file contents associated with the backup file
    /// </summary>
    public byte[] Data { get; }

    /// <summary>
    /// Creates an instance of the BackupFileEventArgs class.
    /// </summary>
    /// <param name="file">The BackupFile related to the event.</param>
    /// <param name="fileSize"></param>
    public BackupFileEventArgs(BackupFile file)
    {
        File = file;
        Data = [];
    }

    /// <summary>
    /// Creates an instance of the BackupFileEventArgs class.
    /// </summary>
    /// <param name="file">The BackupFile related to the event.</param>
    /// <param name="data">The content of the BackupFile</param>
    public BackupFileEventArgs(BackupFile file, byte[] data)
    {
        File = file;
        Data = data;
    }
}

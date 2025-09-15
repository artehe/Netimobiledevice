using System;
using System.Collections.Generic;

namespace Netimobiledevice.Backup;

/// <summary>
/// EventArgs including information about the backup process result.
/// </summary>
public class BackupResultEventArgs(IEnumerable<BackupFile> failedFiles, bool userCancelled, bool deviceDisconnected) : EventArgs
{
    /// <summary>
    /// Indicates whether the user cancelled the backup process.
    /// </summary>
    public bool UserCancelled { get; } = userCancelled;

    /// <summary>
    /// Indicates whether the backup has finished due to a device disconnection.
    /// </summary>
    public bool DeviceDisconnected { get; } = deviceDisconnected;

    /// <summary>
    /// The files that failed to transfer during the backup.
    /// </summary>
    public IReadOnlyList<BackupFile> TransferErrors { get; } = new List<BackupFile>(failedFiles);
}

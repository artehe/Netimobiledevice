using System;
using System.Collections.Generic;

namespace Netimobiledevice.Backup
{
    /// <summary>
    /// EventArgs including information about the backup process result.
    /// </summary>
    public class BackupResultEventArgs : EventArgs
    {
        /// <summary>
        /// Indicates whether the user cancelled the backup process.
        /// </summary>
        public bool UserCancelled { get; }

        /// <summary>
        /// Indicates whether the backup has finished due to a device disconnection.
        /// </summary>
        public bool DeviceDisconnected { get; }

        /// <summary>
        /// The files that failed to transfer during the backup.
        /// </summary>
        public IReadOnlyList<BackupFile> TransferErrors { get; }

        public BackupResultEventArgs(IEnumerable<BackupFile> failedFiles, bool userCancelled, bool deviceDisconnected)
        {
            UserCancelled = userCancelled;
            DeviceDisconnected = deviceDisconnected;
            TransferErrors = new List<BackupFile>(failedFiles);
        }
    }
}

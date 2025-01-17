using System;

namespace Netimobiledevice.Backup
{
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

        public long FileSize { get; }
        /// <summary>
        /// Creates an instance of the BackupFileEventArgs class.
        /// </summary>
        /// <param name="file">The BackupFile related to the event.</param>
        /// <param name="fileSize"></param>
        public BackupFileEventArgs(BackupFile file, long fileSize = 0)
        {
            File = file;
            Data = Array.Empty<byte>();
            FileSize = fileSize;
            
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
}

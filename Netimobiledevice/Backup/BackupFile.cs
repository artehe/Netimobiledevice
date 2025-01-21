using System.IO;

namespace Netimobiledevice.Backup
{
    /// <summary>
    /// Represents a file in the backup process.
    /// </summary>
    public class BackupFile
    {
        /// <summary>
        /// The absolute path in the device.
        /// </summary>
        public string DevicePath { get; }

        /// <summary>
        /// Relative path to the backup folder, includes the device UDID.
        /// </summary>
        public string BackupPath { get; }

        /// <summary>
        /// The absolute path in the local backup folder.
        /// </summary>
        public string LocalPath { get; }

        public long FileSize { get; set; } = 0;
        
        public long ExpectedFileSize { get; set; } = 0;

        /// <summary>
        /// Creates an instance of a BackupFile
        /// </summary>
        /// <param name="devicePath">The absolute path in the device.</param>
        /// <param name="backupPath">Relative path to the backup folder, includes the device UDID.</param>
        /// <param name="backupDirectory">Absolute path to the backup directory</param>
        public BackupFile(string devicePath, string backupPath, string backupDirectory)
        {
            DevicePath = devicePath;
            BackupPath = backupPath;
            LocalPath = Path.Combine(backupDirectory, backupPath);
            Empty = false;
        }
        public BackupFile()
        {
            DevicePath = "";
            BackupPath = "";
            LocalPath = "";
            Empty = true;
        }
        public bool Empty { get; }
    }
}

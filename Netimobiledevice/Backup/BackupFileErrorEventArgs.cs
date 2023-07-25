namespace Netimobiledevice.Backup
{
    /// <summary>
    /// EventArgs for File Transfer Error events.
    /// </summary>
    public class BackupFileErrorEventArgs : BackupFileEventArgs
    {
        /// <summary>
        /// Indicates whether the backup should be cancelled.
        /// </summary>
        public bool Cancel { get; set; }

        public BackupFileErrorEventArgs(BackupFile file) : base(file) { }
    }
}

using System;

namespace Netimobiledevice.Backup
{
    /// <summary>
    /// EventArgs for backup started events.
    /// </summary>
    public class BackupStartedEventArgs : EventArgs
    {
        /// <summary>
        /// The iOS version for the backup
        /// </summary>
        public Version IosVersion { get; }

        /// <summary>
        /// Creates an instance of the BackupStartedEventArgs class.
        /// </summary>
        /// <param name="iosVersion">The iOS version for the device.</param>
        public BackupStartedEventArgs(Version iosVersion)
        {
            IosVersion = iosVersion;
        }
    }
}

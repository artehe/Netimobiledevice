using System;

namespace Netimobiledevice.Backup
{
    /// <summary>
    /// Generic EventArgs for signaling a status message.
    /// </summary>
    public class StatusEventArgs : EventArgs
    {
        /// <summary>
        /// The status message.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// The full status details
        /// </summary>
        public BackupStatus? Status { get; }

        /// <summary>
        /// Creates an instance of the StatusEventArgs class.
        /// </summary>
        /// <param name="message">The status message.</param>
        /// <param name="status">The BackupStatus object containing all the details</param>
        public StatusEventArgs(string message, BackupStatus? status = null)
        {
            Message = message;
            Status = status;
        }
    }
}

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
        /// Creates an instance of the StatusEventArgs class.
        /// </summary>
        /// <param name="message">The status message.</param>
        public StatusEventArgs(string message)
        {
            Message = message;
        }
    }
}

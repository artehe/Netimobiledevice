using System;

namespace Netimobiledevice.Backup;

/// <summary>
/// Generic EventArgs for signaling a status message.
/// </summary>
/// <remarks>
/// Creates an instance of the StatusEventArgs class.
/// </remarks>
/// <param name="message">The status message.</param>
/// <param name="status">The BackupStatus object containing all the details</param>
public class StatusEventArgs(string message, BackupStatus? status = null) : EventArgs
{
    /// <summary>
    /// The status message.
    /// </summary>
    public string Message { get; } = message;

    /// <summary>
    /// The full status details
    /// </summary>
    public BackupStatus? Status { get; } = status;
}

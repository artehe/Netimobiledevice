using System;

namespace Netimobiledevice.Backup;

/// <summary>
/// EventArgs for backup started events.
/// </summary>
/// <remarks>
/// Creates an instance of the BackupStartedEventArgs class.
/// </remarks>
/// <param name="iosVersion">The iOS version for the device.</param>
public class BackupStartedEventArgs(Version iosVersion) : EventArgs {
    /// <summary>
    /// The iOS version for the backup
    /// </summary>
    public Version IosVersion { get; } = iosVersion;
}

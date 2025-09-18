namespace Netimobiledevice.Backup;

/// <summary>
/// The backup snapshot state.
/// </summary>
public enum SnapshotState
{
    /// <summary>
    /// Custom added state to signal the process is waiting for the device to get ready.
    /// </summary>
    Waiting = -2,
    /// <summary>
    /// Custom added state to signal the process has not yet started.
    /// </summary>
    Uninitialized = -1,
    /// <summary>
    /// Status.plist defined state signaling files are being transferred from the device to the host.
    /// </summary>
    Uploading = 0,
    /// <summary>
    /// Status.plist defined state signaling files are moved to its final location on the host.
    /// </summary>
    Moving,
    /// <summary>
    /// Status.plist defined state signaling files are being removed in the host.
    /// </summary>
    Removing,
    /// <summary>
    /// Status.plist defined state signaling that the process has finished.
    /// </summary>
    Finished
}

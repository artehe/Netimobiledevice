namespace Netimobiledevice.Services.Mobilebackup2;

/// <summary>
/// The backup snapshot state.
/// </summary>
public enum SnapshotState {
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

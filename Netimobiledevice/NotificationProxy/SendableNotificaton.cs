namespace Netimobiledevice.NotificationProxy
{
    /// <summary>
    /// Host-To-Device (sendable) notifications.
    /// </summary>
    public enum SendableNotificaton
    {
        /// <summary>
        /// The host notifies the device that it's about to start the backup.
        /// </summary>
        SyncWillStart = 0,
        /// <summary>
        /// The host notifies the device that the backup has started.
        /// </summary>
        SyncDidStart,
        /// <summary>
        /// The host notifies the device that the backup has finished.
        /// </summary>
        SyncDidFinish,
        /// <summary>
        /// The host notifies the device about the lock request.
        /// </summary>
        SyncLockRequest
    }
}

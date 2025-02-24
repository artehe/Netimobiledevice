namespace Netimobiledevice.NotificationProxy
{
    /// <summary>
    /// Host-To-Device (sendable) notifications.
    /// </summary>
    public static class SendableNotificaton
    {
        /// <summary>
        /// The host notifies the device that it's about to start the backup.
        /// </summary>
        public static string SyncWillStart => "com.apple.itunes-mobdev.syncWillStart";
        /// <summary>
        /// The host notifies the device that the backup has started.
        /// </summary>
        public static string SyncDidStart => "com.apple.itunes-mobdev.syncDidStart";
        /// <summary>
        /// The host notifies the device that the backup has finished.
        /// </summary>
        public static string SyncDidFinish => "com.apple.itunes-mobdev.syncDidFinish";
        /// <summary>
        /// The host notifies the device about the lock request.
        /// </summary>
        public static string SyncLockRequest => "com.apple.itunes-mobdev.syncLockRequest";
    }
}

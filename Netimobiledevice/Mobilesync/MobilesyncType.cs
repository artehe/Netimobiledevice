namespace Netimobiledevice.Mobilesync
{
    /// <summary>
    /// What sync type to use for the sync session
    /// </summary>
    internal enum MobilesyncType
    {
        /// <summary>
        /// Requires that only the changes mad since the last synchronization should be reported
        /// </summary>
        SDSyncTypeFast,
        /// <summary>
        /// Requires that all data needs to be synchronized
        /// </summary>
        SDSyncTypeSlow,
        /// <summary>
        /// Signals that the computer should send all data again
        /// </summary>
        SDSyncTypeReset
    }
}

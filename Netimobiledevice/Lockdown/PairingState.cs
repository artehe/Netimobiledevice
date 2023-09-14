namespace Netimobiledevice.Lockdown
{
    /// <summary>
    /// Represent the different possible state for the pairing status.
    /// </summary>
    public enum PairingState
    {
        /// <summary>
        /// The device is paired.
        /// </summary>
        Paired = LockdownError.Success,
        /// <summary>
        /// The device is password protected.
        /// </summary>
        PasswordProtected = LockdownError.PasswordProtected,
        /// <summary>
        /// The user denied the pairing process.
        /// </summary>
        UserDeniedPairing = LockdownError.UserDeniedPairing,
        /// <summary>
        /// Waiting for user to accept the pairing dialog.
        /// </summary>
        PairingDialogResponsePending = LockdownError.PairingDialogResponsePending,
        /// <summary>
        /// The device is not currently paired
        /// </summary>
        Unpaired = LockdownError.PairingFailed
    }
}

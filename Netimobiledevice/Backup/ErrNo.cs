namespace Netimobiledevice.Backup
{
    /// <summary>
    /// C ErrNo error codes
    /// </summary>
    internal enum ErrNo : int
    {
        /// <summary>
        /// No Error.
        /// </summary>
        ENOERR = 0,
        /// <summary>
        /// Not found.
        /// </summary>
        ENOENT = 2,
        /// <summary>
        /// Permission denied.
        /// </summary>
        EACCES = 13,
        /// <summary>
        /// Already exists.
        /// </summary>
        EEXIST = 17,
    }
}

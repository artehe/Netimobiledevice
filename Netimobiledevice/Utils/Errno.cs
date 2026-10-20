using System.Collections.Generic;

namespace Netimobiledevice.Utils;

/// <summary>
/// POSIX errno values in the device's (Darwin) numbering.
/// </summary>
internal static class Errno {
    /// <summary>
    /// No Error.
    /// </summary>
    public const int ENOERR = 0;
    /// <summary>
    /// Not found.
    /// </summary>
    public const int ENOENT = 2;
    public const int EIO = 5;
    /// <summary>
    /// Permission denied.
    /// </summary>
    public const int EACCES = 13;
    /// <summary>
    /// Already exists.
    /// </summary>
    public const int EEXIST = 17;
    public const int ENOTDIR = 20;
    public const int EISDIR = 21;
    public const int ENOSPC = 28;
    public const int ELOOP = 62;

    private static readonly Dictionary<int, long> ErrnoToDeviceError = new() {
        [ENOENT] = -6,
        [EEXIST] = -7,
        [ENOTDIR] = -8,
        [EISDIR] = -9,
        [ELOOP] = -10,
        [EIO] = -11,
        [ENOSPC] = -15,
    };

    /// <summary>
    /// Transslate a POSIX errno (macOS numbering, which is what the device speaks) to an actual device error number we can send
    /// </summary>
    /// <param name="errno"></param>
    /// <returns></returns>
    public static long GetDeviceError(int errno) {
        if (ErrnoToDeviceError.TryGetValue(errno, out long value)) {
            return value;
        }
        return -1;
    }

    public static string GetDeviceErrorString(int errno) => errno switch {
        ENOENT => "No such file or directory",
        EIO => "Input/output error",
        EEXIST => "File exists",
        ENOTDIR => "Not a directory",
        EISDIR => "Is a directory",
        ENOSPC => "No space left on device",
        ELOOP => "Too many levels of symbolic links",
        _ => $"Unknown error: {errno}",
    };
}

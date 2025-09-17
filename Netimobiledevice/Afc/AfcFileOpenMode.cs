namespace Netimobiledevice.Afc;

public enum AfcFileOpenMode : ulong
{
    /// <summary>
    /// r O_RDONLY
    /// </summary>
    ReadOnly = 0x00000001,
    /// <summary>
    /// r+  O_RDWR | O_CREAT
    /// </summary>
    ReadWrite = 0x00000002,
    /// <summary>
    /// w   O_WRONLY | O_CREAT  | O_TRUNC
    /// </summary>
    WriteOnly = 0x00000003,
    /// <summary>
    /// w+  O_RDWR | O_CREAT | O_TRUNC
    /// </summary>
    WriteReadTruncate = 0x00000004,
    /// <summary>
    /// O_WRONLY | O_APPEND | O_CREAT
    /// </summary>
    Append = 0x00000005,
    /// <summary>
    /// a+ O_RDWR | O_APPEND | O_CREAT
    /// </summary>
    ReadAppend = 0x00000006
}

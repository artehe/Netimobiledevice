namespace Netimobiledevice.Afc;

internal enum AfcOpCode : ulong
{
    Status = 0x00000001,
    Data = 0x00000002,
    ReadDir = 0x00000003,
    ReadFile = 0x00000004,
    WriteFile = 0x00000005,
    WritePart = 0x00000006,
    Truncate = 0x00000007,
    RemovePath = 0x00000008,
    MakeDir = 0x00000009,
    GetFileInfo = 0x0000000A,
    GetDeviceInfo = 0x0000000B,
    /// <summary>
    /// tmp file+rename
    /// </summary>
    WriteFileAtomic = 0x0000000C,
    FileRefOpen = 0x0000000D,
    FileRefOpenResult = 0x0000000E,
    FileRefRead = 0x0000000F,
    FileRefWrite = 0x00000010,
    FileRefSeek = 0x00000011,
    FileRefTell = 0x00000012,
    FileRefTellResult = 0x00000013,
    FileRefClose = 0x00000014,
    /// <summary>
    /// ftruncate
    /// </summary>
    FileRefSetFileSize = 0x00000015,
    GetConnectionInfo = 0x00000016,
    SetConnectionOptions = 0x00000017,
    RenamePath = 0x00000018,
    SetFSBlockSize = 0x00000019,
    SetSocketBlockSize = 0x0000001A,
    FileRefLock = 0x0000001B,
    MakeLink = 0x0000001C,
    /// <summary>
    /// Set st_mtime
    /// </summary>
    SetFileTime = 0x0000001E,
}

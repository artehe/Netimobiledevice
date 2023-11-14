namespace Netimobiledevice.Afc
{
    internal enum AfcOpCode : ulong
    {
        Status = 0x00000001,
        Data = 0x00000002, // Data
        ReadDir = 0x00000003, // ReadDir
        ReadFile = 0x00000004, // ReadFile
        WriteFile = 0x00000005, // WriteFile
        WritePart = 0x00000006, // WritePart
        Truncate = 0x00000007, // TruncateFile
        RemovePath = 0x00000008, // RemovePath
        MakeDir = 0x00000009, // MakeDir
        GetFileInfo = 0x0000000a, // GetFileInfo
        GetDeviceInfo = 0x0000000b, // GetDeviceInfo
        WriteFileAtomic = 0x0000000c, // WriteFileAtomic (tmp file+rename)
        FileOpen = 0x0000000d, // FileRefOpen
        FileOpenResult = 0x0000000e, // FileRefOpenResult
        Read = 0x0000000f, // FileRefRead 
        Write = 0x00000010, // FileRefWrite
        FileSeek = 0x00000011, // FileRefSeek 
        FileTell = 0x00000012, // FileRefTell 
        FileTellResult = 0x00000013, // FileRefTellResult 
        FileClose = 0x00000014, // FileRefClose 
        FileSetSize = 0x00000015, // FileRefSetFileSize (ftruncate) 
        GetConInfo = 0x00000016, // GetConnectionInfo 
        SetConOptions = 0x00000017, // SetConnectionOptions 
        RenamePath = 0x00000018, // RenamePath 
        SetFsBs = 0x00000019, // SetFSBlockSize (0x800000) 
        SetSocketBs = 0x0000001A, // SetSocketBlockSize (0x800000) 
        FileLock = 0x0000001B, // FileRefLock 
        MakeLink = 0x0000001C, // MakeLink
        SetFileTime = 0x0000001E, // set st_mtime
    }
}

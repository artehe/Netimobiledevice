namespace Netimobiledevice.Afc
{
    public enum AfcFileOpenMode : ulong
    {
        ReadOnly = 0x00000001, // r O_RDONLY
        ReadWrite = 0x00000002, // r+  O_RDWR | O_CREAT
        WriteOnly = 0x00000003, // w   O_WRONLY | O_CREAT  | O_TRUNC
        WriteReadTruncate = 0x00000004, // w+  O_RDWR | O_CREAT | O_TRUNC
        Append = 0x00000005,  // O_WRONLY | O_APPEND | O_CREAT
        ReadAppend = 0x00000006 // a+ O_RDWR | O_APPEND | O_CREAT
    }
}

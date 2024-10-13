namespace Netimobiledevice.Remoted.Xpc
{
    public enum XpcMessageType : uint
    {
        Null = 0x00001000,
        Bool = 0x00002000,
        Int64 = 0x00003000,
        Uint64 = 0x00004000,
        Double = 0x00005000,
        Pointer = 0x00006000,
        Date = 0x00007000,
        Data = 0x00008000,
        String = 0x00009000,
        Uuid = 0x0000A000,
        Fd = 0x0000B000,
        Shmem = 0x0000C000,
        MachSend = 0x0000D000,
        Array = 0x0000E000,
        Dictionary = 0x0000F000,
        Error = 0x00010000,
        Connection = 0x00011000,
        Endpoint = 0x00012000,
        Serializer = 0x00013000,
        Pipe = 0x00014000,
        MachRecv = 0x00015000,
        Bundle = 0x00016000,
        Service = 0x00017000,
        ServiceInstance = 0x00018000,
        Activity = 0x00019000,
        FileTransfer = 0x0001A000,
    }
}

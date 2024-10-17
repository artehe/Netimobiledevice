using System;

namespace Netimobiledevice.Remoted.Xpc
{
    [Flags]
    public enum XpcFlags : uint
    {
        AlwaysSet = 0x00000001,
        Ping = 0x00000002,
        DataPresent = 0x00000100,
        WantingReply = 0x00010000,
        Reply = 0x00020000,
        FileTxStreamRequest = 0x00100000,
        FileTxStreamResponse = 0x00200000,
        InitHandshake = 0x00400000,
    }
}

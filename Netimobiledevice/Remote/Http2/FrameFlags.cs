using System;

namespace Netimobiledevice.Remote.Http2;

[Flags]
internal enum FrameFlags : byte {
    None = 0,
    /// <summary>
    /// Is reused for SETTINGS and PING frames for ACK
    /// </summary>
    EndStream = 0x01,
    EndHeaders = 0x04,
    Padded = 0x08,
    Priority = 0x20
}

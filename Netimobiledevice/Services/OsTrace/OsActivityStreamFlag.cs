using System;

namespace Netimobiledevice.Services.OsTrace;

/// <summary>
/// Bit flags controlling what an activity stream delivers and how it decodes entries.
/// These are the exact <c>os_activity_stream_flag_t</c> values from Apple's private headers
/// and are OR'd together to form the <c>StreamFlags</c> value passed to <see cref="OsTraceService.SyslogAsync"/>.
/// Based on: https://github.com/limneos/oslog/blob/master/ActivityStreamAPI.h
/// </summary>
[Flags]
public enum OsActivityStreamFlag : uint {
    None = 0,
    ProcessOnly = 0x00000001,
    SkipDecode = 0x00000002,
    Payload = 0x00000004,
    Historical = 0x00000008,
    Callstack = 0x00000010,
    Debug = 0x00000020,
    NoSensitive = 0x00000080,
    Info = 0x00000100,
    Promiscuous = 0x00000200,
}

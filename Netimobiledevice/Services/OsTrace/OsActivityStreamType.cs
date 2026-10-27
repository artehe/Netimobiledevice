namespace Netimobiledevice.Services.OsTrace;

/// <summary>
/// Categories of events that may appear in an activity stream (log messages, signposts,
/// activity transitions, etc.). These are the exact <c>os_activity_stream_type_t</c> values
/// from Apple's private headers.
/// Based on: https://github.com/limneos/oslog/blob/master/ActivityStreamAPI.h
/// </summary>
public enum OsActivityStreamType : uint {
    ActivityCreate = 0x0201,
    ActivityTransition = 0x0202,
    ActivityUserAction = 0x0203,
    TraceMessage = 0x0300,
    LogMessage = 0x0400,
    LegacyLogMessage = 0x0480,
    TimeSync = 0x0500,
    Signpost = 0x0600,
    Loss = 0x0700,
    StatedumpEvent = 0x0A00,
}

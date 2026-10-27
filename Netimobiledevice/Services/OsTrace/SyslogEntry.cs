using System;

namespace Netimobiledevice.Services.OsTrace;

/// <summary>
/// A single decoded log line from the device's activity stream, yielded by <see cref="OsTraceService.SyslogAsync"/>.
/// Bundles the emitting process and thread, the entry's timestamp and severity, the log message text,
/// and the image/offset metadata needed to symbolicate the call site.
/// </summary>
/// <param name="ProcId">
/// Unique process id (the <c>procid</c> activity-stream field); equals <paramref name="Pid"/> in practice on iOS.
/// </param>
/// <param name="ThreadId">
/// Id of the thread that emitted the entry (the <c>thread</c> activity-stream field).
/// </param>
/// <param name="ImageUuid">
/// UUID of the sender image (the one named by <paramref name="ImageName"/>); pair with <paramref name="ImageOffset"/> to symbolicate.
/// </param>
/// <param name="ProcessImageUuid">
/// UUID of the process' main executable (the one named by <paramref name="Filename"/>).
/// </param>
/// <param name="MachTimestamp">
/// Raw high-resolution device timestamp in mach ticks (monotonic; clock domain unverified).
/// </param>
public sealed record SyslogEntry(
    uint Pid,
    DateTime Timestamp,
    SyslogLevel Level,
    string ImageName,
    uint ImageOffset,
    string Filename,
    string Message,
    SyslogLabel? Label = null,
    ulong? ProcId = null,
    ulong? ThreadId = null,
    Guid? ImageUuid = null,
    Guid? ProcessImageUuid = null,
    ulong? MachTimestamp = null
);

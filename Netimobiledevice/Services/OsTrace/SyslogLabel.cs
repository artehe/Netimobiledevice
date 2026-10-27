namespace Netimobiledevice.Services.OsTrace;

/// <summary>The os_log subsystem/category pair an entry was emitted under, when present.</summary>
public sealed record SyslogLabel(
    string Category,
    string Subsystem
);

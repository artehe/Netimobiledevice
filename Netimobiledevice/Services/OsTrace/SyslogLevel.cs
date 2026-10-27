namespace Netimobiledevice.Services.OsTrace;

/// <summary>Severity level of a syslog entry, as carried in the raw stream's level byte.</summary>
public enum SyslogLevel : byte {
    Notice = 0x00,
    Info = 0x01,
    Debug = 0x02,
    UserAction = 0x03,
    Error = 0x10,
    Fault = 0x11
}

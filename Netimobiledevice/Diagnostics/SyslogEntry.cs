using System;

namespace Netimobiledevice.Diagnostics
{
    public record SyslogEntry(int Pid, DateTime Timestamp, SyslogLevel Level, string Imagename, string Filename, string Message, SyslogLabel? Label);
}

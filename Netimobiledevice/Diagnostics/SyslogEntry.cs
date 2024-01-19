using System;

namespace Netimobiledevice.Diagnostics
{
    internal record SyslogEntry(int Pid, DateTime Timestamp, SyslogLevel Level, string Imagename, string Filename, string Message, SyslogLabel? Label);
}

namespace Netimobiledevice.Diagnostics
{
    public enum SyslogLevel : int
    {
        Notice = 0x00,
        Info = 0x01,
        Debug = 0x02,
        UserAction = 0x03,
        Error = 0x10,
        Fault = 0x11
    }
}

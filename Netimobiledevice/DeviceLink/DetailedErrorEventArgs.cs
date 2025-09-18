using System;
using System.IO;

namespace Netimobiledevice.DeviceLink;


public class DetailedErrorEventArgs(Exception exception, string details) : ErrorEventArgs(exception)
{
    public string Details { get; set; } = details;
}

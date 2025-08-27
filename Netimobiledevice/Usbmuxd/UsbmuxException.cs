using Netimobiledevice.Exceptions;
using System;

namespace Netimobiledevice.Usbmuxd;

public class UsbmuxException : NetimobiledeviceException
{
    public UsbmuxException() : base() { }

    public UsbmuxException(string message) : base(message) { }

    public UsbmuxException(string message, Exception innerException) : base(message, innerException) { }
}

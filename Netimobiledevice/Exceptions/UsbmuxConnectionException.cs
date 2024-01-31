using System;

namespace Netimobiledevice.Exceptions
{
    public class UsbmuxConnectionException : NetimobiledeviceException
    {
        public UsbmuxConnectionException(string message, Exception innerException) : base(message, innerException) { }
    }
}

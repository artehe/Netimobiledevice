using System;

namespace Netimobiledevice.Exceptions
{
    public class UsbmuxConnectionException : Exception
    {
        public UsbmuxConnectionException(string message, Exception innerException) : base(message, innerException) { }
    }
}

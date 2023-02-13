using System;

namespace Netimobiledevice.Exceptions
{
    internal class UsbmuxConnectionException : Exception
    {
        public UsbmuxConnectionException(string message, Exception innerException) : base(message, innerException) { }
    }
}

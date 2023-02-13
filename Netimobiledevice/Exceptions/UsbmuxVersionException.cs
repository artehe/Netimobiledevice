using System;

namespace Netimobiledevice.Exceptions
{
    internal class UsbmuxVersionException : Exception
    {
        public UsbmuxVersionException(string message) : base(message) { }
    }
}

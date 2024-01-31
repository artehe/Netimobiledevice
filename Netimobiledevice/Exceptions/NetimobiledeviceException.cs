using System;

namespace Netimobiledevice.Exceptions
{
    public class NetimobiledeviceException : Exception
    {
        public NetimobiledeviceException() { }

        public NetimobiledeviceException(string message) : base(message) { }

        public NetimobiledeviceException(string message, Exception inner) : base(message, inner) { }
    }
}

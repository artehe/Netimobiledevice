using System;

namespace Netimobiledevice.Exceptions
{
    public class NetimobiledeviceException : NetimobiledeviceException
    {
        public NetimobiledeviceException() { }

        public NetimobiledeviceException(string message) : base(message) { }

        public NetimobiledeviceException(string message, Exception inner) : base(message, inner) { }
    }
}

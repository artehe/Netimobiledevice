using System;

namespace Netimobiledevice.Exceptions
{
    public class ServiceStartException : Exception
    {
        public ServiceStartException(string message) : base(message) { }
    }
}

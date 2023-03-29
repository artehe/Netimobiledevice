using System;

namespace Netimobiledevice.Exceptions
{
    public class LockdownException : Exception
    {
        public LockdownException(string message) : base(message) { }
    }
}

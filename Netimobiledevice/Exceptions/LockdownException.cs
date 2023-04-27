using Netimobiledevice.Lockdown;
using System;

namespace Netimobiledevice.Exceptions
{
    public class LockdownException : Exception
    {
        public LockdownError LockdownError { get; } = LockdownError.UnknownError;

        public LockdownException() : base() { }

        public LockdownException(LockdownError lockdownError) : base($"{lockdownError}")
        {
            this.LockdownError = lockdownError;
        }

        public LockdownException(string message) : base(message) { }
    }
}

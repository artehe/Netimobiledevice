using System;

namespace Netimobiledevice.Exceptions
{
    public class PasswordRequiredException : Exception
    {
        public PasswordRequiredException(string message) : base(message) { }
    }
}

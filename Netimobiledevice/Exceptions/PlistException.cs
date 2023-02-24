using System;

namespace Netimobiledevice.Exceptions
{
    public class PlistException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PlistException"/> class.
        /// </summary>
        /// <param name="message">Message.</param>
        public PlistException(string message) : base(message) { }
    }
}

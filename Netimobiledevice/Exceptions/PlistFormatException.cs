using System;

namespace Netimobiledevice.Exceptions
{
    /// <summary>
    /// Property list format exception.
    /// </summary>
    internal class PlistFormatException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PlistFormatException"/> class.
        /// </summary>
        public PlistFormatException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PlistFormatException"/> class.
        /// </summary>
        /// <param name="message">Message.</param>
        public PlistFormatException(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PlistFormatException"/> class.
        /// </summary>
        /// <param name="message">Message.</param>
        /// <param name="inner">Inner.</param>
        public PlistFormatException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}

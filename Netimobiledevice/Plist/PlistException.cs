using Netimobiledevice.Exceptions;

namespace Netimobiledevice.Plist
{
    public class PlistException : NetimobiledeviceException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PlistException"/> class.
        /// </summary>
        /// <param name="message">Message.</param>
        public PlistException(string message) : base(message) { }
    }
}

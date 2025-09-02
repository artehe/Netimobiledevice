using Netimobiledevice.Exceptions;

namespace Netimobiledevice.Plist;

/// <summary>
/// Initializes a new instance of the <see cref="PlistException"/> class.
/// </summary>
/// <param name="message">Message.</param>
public class PlistException(string message) : NetimobiledeviceException(message)
{
}

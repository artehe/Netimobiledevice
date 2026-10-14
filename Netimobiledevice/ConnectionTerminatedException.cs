using System;

namespace Netimobiledevice;

/// <summary>
/// Raised when a connection is terminated abruptly.
/// </summary>
public class ConnectionTerminatedException : NetimobiledeviceException {
    public ConnectionTerminatedException() : base("Connection terminated") { }

    public ConnectionTerminatedException(
        string message,
        Exception? innerException = null
    ) : base(message, innerException) { }
}

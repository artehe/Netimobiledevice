using System;

namespace Netimobiledevice;

public class NetimobiledeviceException : Exception {
    public NetimobiledeviceException() { }

    public NetimobiledeviceException(string message, Exception? inner = null) : base(message, inner) { }
}

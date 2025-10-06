using System;

namespace Netimobiledevice.Remoted;

public class AccessDeniedException : NetimobiledeviceException {
    public AccessDeniedException(string message) : base(message) { }

    public AccessDeniedException(string message, Exception inner) : base(message, inner) { }
}

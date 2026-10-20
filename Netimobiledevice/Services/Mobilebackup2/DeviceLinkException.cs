using System;

namespace Netimobiledevice.Services.Mobilebackup2;

public class DeviceLinkException : NetimobiledeviceException {
    public DeviceLinkException(string message) : base(message) { }

    public DeviceLinkException(string message, Exception innerException) : base(message, innerException) { }
}

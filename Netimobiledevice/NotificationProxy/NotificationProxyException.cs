using Netimobiledevice.Exceptions;

namespace Netimobiledevice.NotificationProxy;

public sealed class NotificationProxyException : NetimobiledeviceException
{
    public NotificationProxyException() { }

    public NotificationProxyException(string message) : base(message) { }
}

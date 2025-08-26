using Netimobiledevice.Exceptions;
using System;

namespace Netimobiledevice.Heartbeat;

public sealed class HeartbeatException : NetimobiledeviceException
{
    public HeartbeatException() { }

    public HeartbeatException(string message) : base(message) { }

    public HeartbeatException(string message, Exception inner) : base(message, inner) { }
}

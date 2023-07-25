using System;

namespace Netimobiledevice.Exceptions
{
    /// <summary>
    /// Exception thrown when the device is disconnected.
    /// </summary>
    public class DeviceDisconnectedException : Exception
    {
        public DeviceDisconnectedException() : base("Device disconnected") { }
    }
}

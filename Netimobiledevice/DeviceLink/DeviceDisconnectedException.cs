namespace Netimobiledevice.DeviceLink;

/// <summary>
/// Exception thrown when the device is disconnected.
/// </summary>
public class DeviceDisconnectedException : NetimobiledeviceException
{
    public DeviceDisconnectedException() : base("Device disconnected") { }
}

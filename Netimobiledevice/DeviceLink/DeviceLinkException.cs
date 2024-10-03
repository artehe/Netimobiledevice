using Netimobiledevice.Exceptions;

namespace Netimobiledevice.DeviceLink
{
    public class DeviceLinkException : NetimobiledeviceException
    {
        public DLResultCode ErrorCode { get; } = DLResultCode.UnexpectedError;

        public DeviceLinkException(DLResultCode error) : base($"{error}")
        {
            ErrorCode = error;
        }

        public DeviceLinkException(DLResultCode error, string message) : base(message)
        {
            ErrorCode = error;
        }

        public DeviceLinkException(string message) : base(message) { }
    }
}

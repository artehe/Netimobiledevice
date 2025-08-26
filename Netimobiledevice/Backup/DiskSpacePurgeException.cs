using Netimobiledevice.Exceptions;

namespace Netimobiledevice.Backup
{
    public class DiskSpacePurgeException : NetimobiledeviceException
    {
        public DiskSpacePurgeException() : base() { }

        public DiskSpacePurgeException(string message) : base(message) { }
    }
}

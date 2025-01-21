using System;
using System.IO;

namespace Netimobiledevice.DeviceLink
{

    public class DetailedErrorEventArgs : ErrorEventArgs
    {
        public string Details { get; set; }
        
        public DetailedErrorEventArgs(Exception exception, string details) : base(exception) {
            Details = details;
        }
    }

}

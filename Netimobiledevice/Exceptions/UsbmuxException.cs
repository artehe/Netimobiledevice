﻿using System;

namespace Netimobiledevice.Exceptions
{
    public class UsbmuxException : NetimobiledeviceException
    {
        public UsbmuxException() : base() { }

        public UsbmuxException(string message) : base(message) { }

        public UsbmuxException(string message, Exception innerException) : base(message, innerException) { }
    }
}

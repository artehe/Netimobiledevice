using Netimobiledevice.Exceptions;
using System;

namespace Netimobiledevice.Usbmuxd;

public class UsbmuxConnectionException(string message, Exception innerException) : NetimobiledeviceException(message, innerException) { }

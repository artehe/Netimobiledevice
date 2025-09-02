using Netimobiledevice.Exceptions;

namespace Netimobiledevice.Diagnostics;

public class DiagnosticsException(string message) : NetimobiledeviceException(message)
{
}

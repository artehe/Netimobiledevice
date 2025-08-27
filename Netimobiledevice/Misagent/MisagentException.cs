using Netimobiledevice.Exceptions;

namespace Netimobiledevice.Misagent;

public sealed class MisagentException(string message) : NetimobiledeviceException(message) { }

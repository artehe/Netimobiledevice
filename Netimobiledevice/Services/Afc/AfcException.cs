using System;

namespace Netimobiledevice.Services.Afc;

/// <summary>
/// An AFC operation failed. <see cref="Status"/> carries the device's AFC error code, if any.
/// </summary>
public class AfcException(
    string message,
    AfcError? status = null,
    string? filename = null,
    Exception? innerException = null
) : NetimobiledeviceException(message, innerException) {
    public string? Filename { get; } = filename;
    public AfcError? Status { get; } = status;
}

using System;

namespace Netimobiledevice.Services.Afc;

/// <summary>The path does not exist on the device.</summary>
public class AfcFileNotFoundException(
    string message,
    AfcError? status = null,
    string? filename = null,
    Exception? innerException = null
) : AfcException(message, status, filename, innerException) { }

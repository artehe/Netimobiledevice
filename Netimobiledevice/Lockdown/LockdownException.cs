namespace Netimobiledevice.Lockdown;

public class LockdownException : NetimobiledeviceException
{
    public LockdownError LockdownError { get; } = LockdownError.UnknownError;

    public LockdownException() : base() { }

    public LockdownException(LockdownError lockdownError) : base($"{lockdownError}")
    {
        LockdownError = lockdownError;
    }

    public LockdownException(string message) : base(message) { }
}

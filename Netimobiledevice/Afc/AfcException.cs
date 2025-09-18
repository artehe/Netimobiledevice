namespace Netimobiledevice.Afc;

public class AfcException : NetimobiledeviceException
{
    public AfcError AfcError { get; } = AfcError.UnknownError;

    public AfcException() : base() { }

    public AfcException(AfcError afcError) : base($"{afcError}")
    {
        AfcError = afcError;
    }

    public AfcException(AfcError afcError, string message) : base(message)
    {
        AfcError = afcError;
    }

    public AfcException(string message) : base(message) { }
}

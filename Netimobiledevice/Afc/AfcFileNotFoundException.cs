namespace Netimobiledevice.Afc
{
    public class AfcFileNotFoundException : AfcException
    {
        public AfcFileNotFoundException(AfcError afcError) : base(afcError)
        {
        }

        public AfcFileNotFoundException(AfcError afcError, string message) : base(afcError, message)
        {
        }
    }
}

using Netimobiledevice.Extentions;

namespace Netimobiledevice.Afc
{
    internal class AfcRmRequest
    {
        public CString Filename { get; }

        public AfcRmRequest(CString filename)
        {
            Filename = filename;
        }

        public byte[] GetBytes()
        {
            return Filename.Bytes;
        }
    }
}

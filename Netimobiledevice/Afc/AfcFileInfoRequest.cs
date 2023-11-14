using Netimobiledevice.Extentions;

namespace Netimobiledevice.Afc
{
    internal class AfcFileInfoRequest
    {
        public CString Filename { get; }

        public AfcFileInfoRequest(CString filename)
        {
            Filename = filename;
        }

        public byte[] GetBytes()
        {
            return Filename.Bytes;
        }
    }
}

namespace Netimobiledevice.Afc.Packets
{
    internal class AfcReadDirectoryRequest : AfcPacket
    {
        public CString Filename { get; }

        public override int DataSize => Filename.Length;

        public AfcReadDirectoryRequest(string filename)
        {
            Filename = new CString(filename);
        }

        public override byte[] GetBytes()
        {
            return Filename.Bytes;
        }
    }
}

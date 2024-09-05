namespace Netimobiledevice.Afc.Packets
{
    internal class AfcFileInfoRequest : AfcPacket
    {
        public CString Filename { get; set; }

        public override int DataSize => GetBytes().Length;

        public AfcFileInfoRequest(string filename)
        {
            Filename = new CString(filename);
        }

        public override byte[] GetBytes()
        {
            return Filename.Bytes;
        }
    }
}

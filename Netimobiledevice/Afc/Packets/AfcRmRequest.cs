namespace Netimobiledevice.Afc.Packets
{
    internal class AfcRmRequest : AfcPacket
    {
        public CString Filename { get; }

        public override int DataSize => GetBytes().Length;

        public AfcRmRequest(string filename)
        {
            Filename = new CString(filename);
        }

        public override byte[] GetBytes()
        {
            return Filename.Bytes;
        }
    }
}

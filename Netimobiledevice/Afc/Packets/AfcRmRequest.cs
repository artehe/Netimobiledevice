using Netimobiledevice.Extentions;
using System.Text;

namespace Netimobiledevice.Afc.Packets
{
    internal class AfcRmRequest(string filename) : AfcPacket
    {
        public CString Filename { get; } = filename.AsCString(Encoding.UTF8);

        public override int DataSize => Filename.Length;

        public override byte[] GetBytes()
        {
            return [
                .. Header.GetBytes(),
                .. Filename.GetBytes()
            ];
        }
    }
}

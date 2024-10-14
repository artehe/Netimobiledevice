using Netimobiledevice.Extentions;
using System.Text;

namespace Netimobiledevice.Afc.Packets
{
    internal class AfcReadDirectoryRequest(string filename) : AfcPacket
    {
        public string Filename { get; } = filename;

        public override int DataSize => Filename.AsCString().Length;

        public override byte[] GetBytes()
        {
            return [.. Header.GetBytes(), .. Filename.AsCString().GetBytes(Encoding.UTF8)];
        }
    }
}

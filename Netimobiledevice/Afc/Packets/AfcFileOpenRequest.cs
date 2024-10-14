using Netimobiledevice.Extentions;
using System;
using System.Text;

namespace Netimobiledevice.Afc.Packets
{
    internal class AfcFileOpenRequest(AfcFileOpenMode mode, string filename) : AfcPacket
    {
        public AfcFileOpenMode Mode { get; } = mode;
        public string Filename { get; } = filename;

        public override int DataSize => sizeof(AfcFileOpenMode) + Filename.AsCString().Length;

        public override byte[] GetBytes()
        {
            return [
                .. Header.GetBytes(),
                .. BitConverter.GetBytes((ulong) Mode),
                .. Filename.AsCString().GetBytes(Encoding.UTF8),
            ];
        }
    }
}

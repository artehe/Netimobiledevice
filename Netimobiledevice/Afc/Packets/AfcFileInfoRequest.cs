using Netimobiledevice.Extentions;
using System.Text;

namespace Netimobiledevice.Afc.Packets;

internal class AfcFileInfoRequest(string filename) : AfcPacket
{
    public CString Filename { get; set; } = filename.AsCString(Encoding.UTF8);

    public override int DataSize => Filename.Length;

    public override byte[] GetBytes()
    {
        return [
            .. Header.GetBytes(),
            .. Filename.GetBytes()
        ];
    }
}

using Netimobiledevice.Extentions;
using System;
using System.Text;

namespace Netimobiledevice.Afc.Packets;

internal class AfcFileOpenRequest(AfcFileOpenMode mode, string filename) : AfcPacket
{
    public AfcFileOpenMode Mode { get; } = mode;
    public CString Filename { get; } = filename.AsCString(Encoding.UTF8);

    public override int DataSize => sizeof(AfcFileOpenMode) + Filename.Length;

    public override byte[] GetBytes()
    {
        return [
            .. Header.GetBytes(),
            .. BitConverter.GetBytes((ulong) Mode),
            .. Filename.GetBytes(),
        ];
    }
}

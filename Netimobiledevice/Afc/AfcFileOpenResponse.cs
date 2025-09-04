using Netimobiledevice.EndianBitConversion;

namespace Netimobiledevice.Afc;

internal class AfcFileOpenResponse
{
    public ulong Handle { get; set; }

    public static AfcFileOpenResponse FromBytes(byte[] bytes)
    {
        return new AfcFileOpenResponse() {
            Handle = EndianBitConverter.LittleEndian.ToUInt64(bytes, 0)
        };
    }
}

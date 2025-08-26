using System;

namespace Netimobiledevice.EndianBitConversion;

internal static class EndianNetworkConverter
{
    public static ushort HostToNetworkOrder(ushort value)
    {
        if (BitConverter.IsLittleEndian) {
            byte[] bytes = BitConverter.GetBytes(value);
            return EndianBitConverter.BigEndian.ToUInt16(bytes, 0);
        }
        else {
            return value;
        }
    }
}

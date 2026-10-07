using System;
using System.Buffers.Binary;

namespace Netimobiledevice.Utils;

internal static class EndianNetworkConverter {
    public static ushort HostToNetworkOrder(ushort value) => BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
}


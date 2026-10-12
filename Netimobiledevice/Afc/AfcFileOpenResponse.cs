using System.Buffers.Binary;

namespace Netimobiledevice.Afc;

internal class AfcFileOpenResponse {
    public ulong Handle { get; set; }

    public static AfcFileOpenResponse FromBytes(byte[] bytes) {
        return new AfcFileOpenResponse() {
            Handle = BinaryPrimitives.ReadUInt64LittleEndian(bytes)
        };
    }
}

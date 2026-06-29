using System.IO;

namespace Netimobiledevice.Plist;

internal sealed class BinaryReaderState(Stream stream, int[] nodeOffsets, int indexSize, int objectRefSize) {
    public Stream Stream { get; } = stream;
    public int[] NodeOffsets { get; } = nodeOffsets;
    public int OffsetIntSize { get; } = indexSize;
    public int ObjectRefSize { get; } = objectRefSize;
}

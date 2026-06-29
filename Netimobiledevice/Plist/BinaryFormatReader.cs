using Netimobiledevice.EndianBitConversion;
using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Netimobiledevice.Plist;

/// <summary>
/// A class, used to read binary formated <see cref="PropertyNode"/> from a stream
/// </summary>
internal sealed class BinaryFormatReader {
    private static ulong GetNodeOffset(BinaryReaderState readerState, byte[] bufKeys, int index) => readerState.ObjectRefSize switch {
        1 => bufKeys[index],
        2 => EndianBitConverter.BigEndian.ToUInt16(bufKeys, readerState.ObjectRefSize * index),
        4 => EndianBitConverter.BigEndian.ToUInt32(bufKeys, readerState.ObjectRefSize * index),
        8 => EndianBitConverter.BigEndian.ToUInt64(bufKeys, readerState.ObjectRefSize * index),
        _ => throw new PlistFormatException($"Unexpected index size: {readerState.ObjectRefSize}."),
    };

    private static NodeTagAndLength GetObjectLengthAndTag(Stream stream) {
        // Read the marker byte
        // left 4 bits represent the tag, which indicates the node type
        // right 4 bits indicate the length
        //  - if size fits in 4 bits, the number is the length
        //  - if the bit value is 1111, the following byte will contain information needed to decode length as follows:
        //      - 4 left bits are 0001
        //      - 4 right bits is the power of 2 required to represent the length
        //      - the following pow(2, x) bytes give us the length (big-endian)

        Span<byte> markerBuf = stackalloc byte[1];
        stream.ReadExactly(markerBuf);

        byte tag = markerBuf[0];
        int length = markerBuf[0] & 0x0F;

        // Length fits in 4 bits, return
        if (length != 0xF) {
            return new NodeTagAndLength(tag, length);
        }
        return new NodeTagAndLength(tag, ReadExtendedLength(stream));
    }

    private static int ReadExtendedLength(Stream stream) {
        // Read next byte to determine the length (in bytes) of actual length value
        Span<byte> headerBuf = stackalloc byte[1];
        stream.ReadExactly(headerBuf);

        // Verify that leftmost bits are 0001
        if ((headerBuf[0] & 0xF0) != 0x10) {
            throw new PlistFormatException("Invalid node length byte header.");
        }

        // Guard against absurd/malicious encodings before allocating or reading.
        // 2^2 = 4 bytes covers any realistic plist length; allow up to 2^3 = 8
        // bytes for safety margin, but reject anything larger.
        int powerOfTwo = headerBuf[0] & 0x0F;
        if (powerOfTwo > 3) {
            throw new PlistFormatException($"Unsupported node length encoding: 2^{powerOfTwo} bytes.");
        }

        // Now get the length
        int byteCount = 1 << powerOfTwo;
        Span<byte> lengthBuffer = stackalloc byte[byteCount];
        stream.ReadExactly(lengthBuffer);

        long length = ReadNumber(lengthBuffer);
        if (length is < 0 or > int.MaxValue) {
            throw new PlistFormatException($"Node length {length} is out of supported range.");
        }
        return (int) length;
    }


    private void ReadInArray(ArrayNode node, int nodeLength, BinaryReaderState readerState) {
        byte[] buf = new byte[nodeLength * readerState.ObjectRefSize];
        readerState.Stream.ReadExactly(buf);
        for (int i = 0; i < nodeLength; i++) {
            ulong topNode = GetNodeOffset(readerState, buf, i);
            node.Add(ReadInternal(readerState, topNode));
        }
    }

    private async Task ReadInArrayAsync(ArrayNode node, int nodeLength, BinaryReaderState readerState) {
        byte[] buf = new byte[nodeLength * readerState.ObjectRefSize];
        await readerState.Stream.ReadExactlyAsync(buf);
        for (int i = 0; i < nodeLength; i++) {
            ulong topNode = GetNodeOffset(readerState, buf, i);
            node.Add(await ReadInternalAsync(readerState, topNode).ConfigureAwait(false));
        }
    }

    private void ReadInDictionary(DictionaryNode node, int nodeLength, BinaryReaderState readerState) {
        byte[] bufKeys = new byte[nodeLength * readerState.ObjectRefSize];
        readerState.Stream.ReadExactly(bufKeys);

        byte[] bufVals = new byte[nodeLength * readerState.ObjectRefSize];
        readerState.Stream.ReadExactly(bufVals);

        for (int i = 0; i < nodeLength; i++) {
            ulong topNode = GetNodeOffset(readerState, bufKeys, i);
            PropertyNode plKey = ReadInternal(readerState, topNode);

            if (plKey is not StringNode stringKey) {
                throw new PlistFormatException("Key is not a string");
            }

            topNode = GetNodeOffset(readerState, bufVals, i);
            PropertyNode plVal = ReadInternal(readerState, topNode);

            node.Add(stringKey.Value, plVal);
        }
    }

    private async Task ReadInDictionaryAsync(DictionaryNode node, int nodeLength, BinaryReaderState readerState) {
        byte[] bufKeys = new byte[nodeLength * readerState.ObjectRefSize];
        await readerState.Stream.ReadExactlyAsync(bufKeys).ConfigureAwait(false);

        byte[] bufVals = new byte[nodeLength * readerState.ObjectRefSize];
        await readerState.Stream.ReadExactlyAsync(bufVals).ConfigureAwait(false);

        for (int i = 0; i < nodeLength; i++) {
            ulong topNode = GetNodeOffset(readerState, bufKeys, i);
            PropertyNode plKey = await ReadInternalAsync(readerState, topNode).ConfigureAwait(false);

            if (plKey is not StringNode stringKey) {
                throw new PlistFormatException("Key is not a string");
            }

            topNode = GetNodeOffset(readerState, bufVals, i);
            PropertyNode plVal = await ReadInternalAsync(readerState, topNode).ConfigureAwait(false);

            node.Add(stringKey.Value, plVal);
        }
    }

    /// <summary>
    /// Reads the <see cref="PropertyNode"/> at the specified idx.
    /// </summary>
    /// <param name="readerState">Reader state.</param>
    /// <param name="elemIdx">The elem idx.</param>
    /// <returns>The <see cref="PropertyNode"/> at the specified idx.</returns>
    private PropertyNode ReadInternal(BinaryReaderState readerState, ulong elemIdx) {
        readerState.Stream.Seek(readerState.NodeOffsets[elemIdx], SeekOrigin.Begin);
        return ReadInternal(readerState);
    }

    /// <summary>
    /// Reads the <see cref="PropertyNode"/> at the current stream position.
    /// </summary>
    /// <param name="readerState">Reader state.</param>
    /// <returns>The <see cref="PropertyNode"/> at the current stream position.</returns>
    private PropertyNode ReadInternal(BinaryReaderState readerState) {
        NodeTagAndLength tagAndLength = GetObjectLengthAndTag(readerState.Stream);

        byte tag = tagAndLength.Tag;
        int objectLength = tagAndLength.Length;

        PropertyNode node = NodeFactory.Create(tag, objectLength);

        // array and dictionary are special-cased here
        // while primitives handle their own loading
        if (node is ArrayNode arrayNode) {
            ReadInArray(arrayNode, objectLength, readerState);
            return node;
        }

        if (node is DictionaryNode dictionaryNode) {
            ReadInDictionary(dictionaryNode, objectLength, readerState);
            return node;
        }

        node.ReadBinary(readerState.Stream, objectLength);

        return node;
    }

    /// <summary>
    /// Reads the <see cref="PropertyNode"/> at the specified idx.
    /// </summary>
    /// <param name="readerState">Reader state.</param>
    /// <param name="elemIdx">The elem idx.</param>
    /// <returns>The <see cref="PropertyNode"/> at the specified idx.</returns>
    private Task<PropertyNode> ReadInternalAsync(BinaryReaderState readerState, ulong elemIdx) {
        readerState.Stream.Seek(readerState.NodeOffsets[elemIdx], SeekOrigin.Begin);
        return ReadInternalAsync(readerState);
    }

    /// <summary>
    /// Reads the <see cref="PropertyNode"/> at the current stream position.
    /// </summary>
    /// <param name="readerState">Reader state.</param>
    /// <returns>The <see cref="PropertyNode"/> at the current stream position.</returns>
    private async Task<PropertyNode> ReadInternalAsync(BinaryReaderState readerState) {
        NodeTagAndLength tagAndLength = GetObjectLengthAndTag(readerState.Stream);

        byte tag = tagAndLength.Tag;
        int objectLength = tagAndLength.Length;

        PropertyNode node = NodeFactory.Create(tag, objectLength);

        // array and dictionary are special-cased here
        // while primitives handle their own loading
        if (node is ArrayNode arrayNode) {
            await ReadInArrayAsync(arrayNode, objectLength, readerState).ConfigureAwait(false);
            return node;
        }

        if (node is DictionaryNode dictionaryNode) {
            await ReadInDictionaryAsync(dictionaryNode, objectLength, readerState).ConfigureAwait(false);
            return node;
        }

        await node.ReadBinaryAsync(readerState.Stream, objectLength).ConfigureAwait(false);

        return node;
    }

    /// <summary>
    ///	Read in offsets. Converting to Int32 because .NET Stream.Read method takes Int32s.
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="trailer"></param>
    /// <returns></returns>
    private static int[] ReadNodeOffsets(Stream stream, PlistTrailer trailer) {
        // The bitconverter library we use only knows how to deal with integer offsets
        if (trailer.NumObjects > int.MaxValue) {
            throw new PlistFormatException($"Offset table contains too many entries: {trailer.NumObjects}.");
        }

        // Position the stream at the start of the offset table
        if (stream.Seek((long) trailer.OffsetTableOffset, SeekOrigin.Begin) != (long) trailer.OffsetTableOffset) {
            throw new PlistFormatException("Invalid plist file: unable to seek to start of the offset table.");
        }

        byte offsetSize = trailer.OffsetIntSize;
        byte[] buffer = new byte[offsetSize];
        int[] nodeOffsets = new int[trailer.NumObjects];

        for (ulong i = 0; i < trailer.NumObjects; i++) {
            // Read's value {i} of the offset table
            stream.ReadExactly(buffer);
            nodeOffsets[i] = ReadNumber(buffer);
        }

        return nodeOffsets;
    }

    /// <summary>
    ///	Read in offsets. Converting to Int32 because .NET Stream.Read method takes Int32s.
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="trailer"></param>
    /// <returns></returns>
    private static async Task<int[]> ReadNodeOffsetsAsync(Stream stream, PlistTrailer trailer) {
        // The bitconverter library we use only knows how to deal with integer offsets
        if (trailer.NumObjects > int.MaxValue) {
            throw new PlistFormatException($"Offset table contains too many entries: {trailer.NumObjects}.");
        }

        // Position the stream at the start of the offset table
        if (stream.Seek((long) trailer.OffsetTableOffset, SeekOrigin.Begin) != (long) trailer.OffsetTableOffset) {
            throw new PlistFormatException("Invalid plist file: unable to seek to start of the offset table.");
        }

        byte offsetSize = trailer.OffsetIntSize;
        byte[] buffer = new byte[offsetSize];
        int[] nodeOffsets = new int[trailer.NumObjects];

        for (ulong i = 0; i < trailer.NumObjects; i++) {
            // Read value {i} in the offset table
            await stream.ReadExactlyAsync(buffer).ConfigureAwait(false);
            nodeOffsets[i] = ReadNumber(buffer);
        }

        return nodeOffsets;
    }

    private static int ReadNumber(ReadOnlySpan<byte> buffer) => buffer.Length switch {
        1 => buffer[0],
        2 => BinaryPrimitives.ReadUInt16BigEndian(buffer),
        4 => (int) BinaryPrimitives.ReadUInt32BigEndian(buffer),
        8 => (int) BinaryPrimitives.ReadUInt64BigEndian(buffer),
        _ => throw new PlistFormatException($"Unexpected offset int size: {buffer.Length}."),
    };

    private static PlistTrailer ReadTrailer(Stream stream) {
        // Trailer is 32 bytes long, at the end of the file
        byte[] buffer = new byte[32];
        stream.Seek(-32, SeekOrigin.End);
        stream.ReadExactly(buffer);

        // all data in a binary plist file is big-endian
        EndianBitConverter converter = EndianBitConverter.BigEndian;
        var trailer = new PlistTrailer {
            Unused = [.. buffer[..5]],
            SortVersionl = buffer[5],
            OffsetIntSize = buffer[6],
            ObjectRefSize = buffer[7],
            NumObjects = converter.ToUInt64(buffer, 8),
            TopObject = converter.ToUInt64(buffer, 16),
            OffsetTableOffset = converter.ToUInt64(buffer, 24)
        };

        return trailer;
    }

    private static async Task<PlistTrailer> ReadTrailerAsync(Stream stream) {
        // Trailer is 32 bytes long, at the end of the file
        byte[] buffer = new byte[32];
        stream.Seek(-32, SeekOrigin.End);
        await stream.ReadExactlyAsync(buffer).ConfigureAwait(false);

        // all data in a binary plist file is big-endian
        EndianBitConverter converter = EndianBitConverter.BigEndian;
        var trailer = new PlistTrailer {
            Unused = new byte[5],
            SortVersionl = buffer[5],
            OffsetIntSize = buffer[6],
            ObjectRefSize = buffer[7],
            NumObjects = converter.ToUInt64(buffer, 8),
            TopObject = converter.ToUInt64(buffer, 16),
            OffsetTableOffset = converter.ToUInt64(buffer, 24)
        };

        return trailer;
    }

    private static void ValidatePlistFileHeader(Stream stream) {
        stream.Seek(0, SeekOrigin.Begin);

        // File must start with an 8-byte header.
        byte[] buffer = new byte[8];
        stream.ReadExactly(buffer);

        // Get first 6 bytes and match to expected text, "bplist"
        if (!buffer.AsSpan(0, 6).SequenceEqual("bplist"u8)) {
            throw new PlistFormatException("Invalid plist file: must start with string \"bplist\".");
        }
    }

    private static async Task ValidatePlistFileHeaderAsync(Stream stream) {
        stream.Seek(0, SeekOrigin.Begin);

        // File must start with an 8-byte header.
        byte[] buffer = new byte[8];
        await stream.ReadExactlyAsync(buffer).ConfigureAwait(false);

        // Get first 6 bytes and match to expected text, "bplist"
        if (!buffer.AsSpan(0, 6).SequenceEqual("bplist"u8)) {
            throw new PlistFormatException("Invalid plist file: must start with string \"bplist\".");
        }
    }

    /// <summary>
    /// Reads a binary formated <see cref="PropertyNode"/> from the specified stream.
    /// </summary>
    /// <param name="stream">The stream.</param>
    /// <returns>The <see cref="PropertyNode"/>, read from the specified stream</returns>
    public PropertyNode Read(Stream stream) {
        // reference material: https://medium.com/@karaiskc/understanding-apples-binary-property-list-format-281e6da00dbd

        // Read in file header and verify expected bits are found
        ValidatePlistFileHeader(stream);

        // Read in file trailer
        PlistTrailer trailer = ReadTrailer(stream);

        // Read in node offsets
        int[] nodeOffsets = ReadNodeOffsets(stream, trailer);
        BinaryReaderState readerState = new BinaryReaderState(stream, nodeOffsets, trailer.OffsetIntSize, trailer.ObjectRefSize);

        return ReadInternal(readerState, trailer.TopObject);
    }

    /// <summary>
    /// Reads a binary formated <see cref="PropertyNode"/> from the specified stream.
    /// </summary>
    /// <param name="stream">The stream.</param>
    /// <returns>The <see cref="PropertyNode"/>, read from the specified stream</returns>
    public async Task<PropertyNode> ReadAsync(Stream stream) {
        // reference material: https://medium.com/@karaiskc/understanding-apples-binary-property-list-format-281e6da00dbd

        // Read in file header and verify expected bits are found
        await ValidatePlistFileHeaderAsync(stream).ConfigureAwait(false);

        // Read in file trailer
        PlistTrailer trailer = await ReadTrailerAsync(stream).ConfigureAwait(false);

        // Read in node offsets
        int[] nodeOffsets = await ReadNodeOffsetsAsync(stream, trailer).ConfigureAwait(false);
        BinaryReaderState readerState = new BinaryReaderState(stream, nodeOffsets, trailer.OffsetIntSize, trailer.ObjectRefSize);

        return await ReadInternalAsync(readerState, trailer.TopObject).ConfigureAwait(false);
    }
}

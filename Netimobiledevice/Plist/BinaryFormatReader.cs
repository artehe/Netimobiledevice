using Netimobiledevice.EndianBitConversion;
using Netimobiledevice.Exceptions;
using System.Text;

namespace Netimobiledevice.Plist;

/// <summary>
/// A class, used to read binary formated <see cref="PropertyNode"/> from a stream
/// </summary>
internal class BinaryFormatReader
{
    private class ReaderState
    {
        public Stream Stream { get; }
        public int[] NodeOffsets { get; }
        public int OffsetIntSize { get; }
        public int ObjectRefSize { get; }

        public ReaderState(Stream stream, int[] nodeOffsets, int indexSize, int objectRefSize)
        {
            Stream = stream;
            NodeOffsets = nodeOffsets;
            OffsetIntSize = indexSize;
            ObjectRefSize = objectRefSize;
        }
    }

    private static ulong GetNodeOffset(ReaderState readerState, byte[] bufKeys, int index)
    {
        switch (readerState.ObjectRefSize) {
            case 1:
                return bufKeys[index];

            case 2:
                return EndianBitConverter.BigEndian.ToUInt16(bufKeys, readerState.ObjectRefSize * index);

            case 4:
                return EndianBitConverter.BigEndian.ToUInt32(bufKeys, readerState.ObjectRefSize * index);

            case 8:
                return EndianBitConverter.BigEndian.ToUInt64(bufKeys, readerState.ObjectRefSize * index);
        }

        throw new PlistFormatException("$Unexpected index size: {readerState.IndexSize}.");
    }

    private static NodeTagAndLength GetObjectLengthAndTag(Stream stream)
    {
        // Read the marker byte
        // left 4 bits represent the tag, which indicates the node type
        // right 4 bits indicate the length
        //  - if size fits in 4 bits, the number is the length
        //  - if the bit value is 1111, the following byte will contain information needed to decode length as follows:
        //      - 4 left bits are 0001
        //      - 4 right bits is the power of 2 required to represent the length
        //      - the following pow(2, x) bytes give us the length (big-endian)
        byte[] buf = new byte[1];
        if (stream.Read(buf, 0, buf.Length) != buf.Length) {
            throw new PlistFormatException("Couldn't read node tag byte.");
        }

        byte tag = (byte) ((buf[0] >> 4) & 0x0F);
        int length = buf[0] & 0x0F;

        // Length fits in 4 bits, return
        if (length != 0xF) {
            return new NodeTagAndLength(tag, length);
        }

        // Read next byte to determine the length (in bytes) of actual length value
        if (stream.Read(buf, 0, buf.Length) != buf.Length) {
            throw new PlistFormatException("Couldn't read node length byte.");
        }

        // Verify that leftmost bits are 0001
        if (((buf[0] >> 4) & 0x0F) != 0x1) {
            throw new PlistFormatException("Invalid node length byte header.");
        }

        // Get the rightmost bits, giving us the number of bytes (power of 2) that we need
        int byteCount = (int) Math.Pow(2, buf[0] & 0x0F);

        // Now get the length
        byte[] lengthBuffer = new byte[byteCount];
        if (stream.Read(lengthBuffer, 0, lengthBuffer.Length) != lengthBuffer.Length) {
            throw new PlistFormatException("Couldn't read node length byte(s).");
        }
        length = ReadNumber(lengthBuffer, EndianBitConverter.BigEndian);

        return new NodeTagAndLength(tag, length);
    }

    private void ReadInArray(ICollection<PropertyNode> node, int nodeLength, ReaderState readerState)
    {
        byte[] buf = new byte[nodeLength * readerState.ObjectRefSize];
        if (readerState.Stream.Read(buf, 0, buf.Length) != buf.Length) {
            throw new PlistFormatException();
        }

        for (int i = 0; i < nodeLength; i++) {
            var topNode = GetNodeOffset(readerState, buf, i);
            node.Add(ReadInternal(readerState, topNode));
        }
    }

    private void ReadInDictionary(IDictionary<string, PropertyNode> node, int nodeLength, ReaderState readerState)
    {
        var bufKeys = new byte[nodeLength * readerState.ObjectRefSize];
        var bufVals = new byte[nodeLength * readerState.ObjectRefSize];

        if (readerState.Stream.Read(bufKeys, 0, bufKeys.Length) != bufKeys.Length) {
            throw new PlistFormatException();
        }

        if (readerState.Stream.Read(bufVals, 0, bufVals.Length) != bufVals.Length) {
            throw new PlistFormatException();
        }

        for (var i = 0; i < nodeLength; i++) {
            var topNode = GetNodeOffset(readerState, bufKeys, i);
            var plKey = ReadInternal(readerState, topNode);

            var stringKey = plKey as StringNode;
            if (stringKey == null) {
                throw new PlistFormatException("Key is not a string");
            }

            topNode = GetNodeOffset(readerState, bufVals, i);
            var plVal = ReadInternal(readerState, topNode);

            node.Add(stringKey.Value, plVal);
        }
    }

    /// <summary>
    /// Reads the <see cref="PropertyNode"/> at the specified idx.
    /// </summary>
    /// <param name="readerState">Reader state.</param>
    /// <param name="elemIdx">The elem idx.</param>
    /// <returns>The <see cref="PropertyNode"/> at the specified idx.</returns>
    private PropertyNode ReadInternal(ReaderState readerState, ulong elemIdx)
    {
        readerState.Stream.Seek(readerState.NodeOffsets[elemIdx], SeekOrigin.Begin);
        return ReadInternal(readerState);
    }

    /// <summary>
    /// Reads the <see cref="PropertyNode"/> at the current stream position.
    /// </summary>
    /// <param name="readerState">Reader state.</param>
    /// <returns>The <see cref="PropertyNode"/> at the current stream position.</returns>
    private PropertyNode ReadInternal(ReaderState readerState)
    {
        NodeTagAndLength tagAndLength = GetObjectLengthAndTag(readerState.Stream);

        byte tag = tagAndLength.Tag;
        int objectLength = tagAndLength.Length;

        PropertyNode node = NodeFactory.Create(tag, objectLength);

        // array and dictionary are special-cased here
        // while primitives handle their own loading
        ArrayNode? arrayNode = node as ArrayNode;
        if (arrayNode != null) {
            ReadInArray(arrayNode, objectLength, readerState);
            return node;
        }

        DictionaryNode? dictionaryNode = node as DictionaryNode;
        if (dictionaryNode != null) {
            ReadInDictionary(dictionaryNode, objectLength, readerState);
            return node;
        }

        node.ReadBinary(readerState.Stream, objectLength);

        return node;
    }

    /// <summary>
    ///	Read in offsets. Converting to Int32 because .NET Stream.Read method takes Int32s.
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="trailer"></param>
    /// <returns></returns>
    private static int[] ReadNodeOffsets(Stream stream, PlistTrailer trailer)
    {
        // The bitconverter library we use only knows how to deal with integer offsets
        if (trailer.NumObjects > int.MaxValue) {
            throw new PlistFormatException($"Offset table contains too many entries: {trailer.NumObjects}.");
        }

        EndianBitConverter converter = EndianBitConverter.BigEndian;

        // Position the stream at the start of the offset table
        if (stream.Seek((long) trailer.OffsetTableOffset, SeekOrigin.Begin) != (long) trailer.OffsetTableOffset) {
            throw new PlistFormatException("Invalid plist file: unable to seek to start of the offset table.");
        }

        byte offsetSize = trailer.OffsetIntSize;
        byte[] buffer = new byte[offsetSize];
        int[] nodeOffsets = new int[trailer.NumObjects];

        for (ulong i = 0; i < trailer.NumObjects; i++) {
            if (stream.Read(buffer, 0, buffer.Length) != buffer.Length) {
                throw new PlistFormatException($"Invalid plist file: unable to read value {i} in the offset table.");
            }

            nodeOffsets[i] = ReadNumber(buffer, converter);
        }

        return nodeOffsets;
    }

    private static int ReadNumber(byte[] buffer, EndianBitConverter converter)
    {
        switch (buffer.Length) {
            case 1: {
                return buffer[0];
            }
            case 2: {
                return converter.ToUInt16(buffer, 0);
            }
            case 4: {
                return (int) converter.ToUInt32(buffer, 0);
            }
            case 8: {
                return (int) converter.ToUInt64(buffer, 0);
            }
            default: {
                throw new PlistFormatException($"Unexpected offset int size: {buffer.Length}.");
            }
        }
    }

    private static PlistTrailer ReadTrailer(Stream stream)
    {
        // Trailer is 32 bytes long, at the end of the file
        byte[] buffer = new byte[32];
        stream.Seek(-32, SeekOrigin.End);
        if (stream.Read(buffer, 0, buffer.Length) != buffer.Length) {
            throw new PlistFormatException("Invalid plist file: unable to read trailer.");
        }

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

    private static void ValidatePlistFileHeader(Stream stream)
    {
        stream.Seek(0, SeekOrigin.Begin);

        byte[] buffer = new byte[8];
        if (stream.Read(buffer, 0, buffer.Length) != buffer.Length) {
            throw new PlistFormatException("Invalid plist file: must start with 8-byte header.");
        }

        // Get first 6 bytes and match to expected text, "bplist"
        string text = Encoding.UTF8.GetString(buffer, 0, 6);
        if (text != "bplist") {
            throw new PlistFormatException("Invalid plist file: must start with string \"bplist\".");
        }

        // TODO: get version (ASCII numbers in bytes 7 and 8) and pass back to the parser			
    }

    /// <summary>
    /// Reads a binary formated <see cref="PropertyNode"/> from the specified stream.
    /// </summary>
    /// <param name="stream">The stream.</param>
    /// <returns>The <see cref="PropertyNode"/>, read from the specified stream</returns>
    public PropertyNode Read(Stream stream)
    {
        // reference material: https://medium.com/@karaiskc/understanding-apples-binary-property-list-format-281e6da00dbd

        // Read in file header and verify expected bits are found
        ValidatePlistFileHeader(stream);

        // Read in file trailer
        PlistTrailer trailer = ReadTrailer(stream);

        // Read in node offsets
        int[] nodeOffsets = ReadNodeOffsets(stream, trailer);
        var readerState = new ReaderState(stream, nodeOffsets, trailer.OffsetIntSize, trailer.ObjectRefSize);

        return ReadInternal(readerState, trailer.TopObject);
    }
}

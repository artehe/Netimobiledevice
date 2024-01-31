using Netimobiledevice.EndianBitConversion;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Netimobiledevice.Plist
{
    /// <summary>
    /// A class used to write a <see cref="PropertyNode"/> in the binary format to a stream 
    /// </summary>
    internal class BinaryFormatWriter
    {
        /// <summary>
		/// The Header (bplist00)
		/// </summary>
		private static readonly byte[] _header = {
            0x62, 0x70, 0x6c, 0x69, 0x73, 0x74, 0x30, 0x30
        };

        private readonly Dictionary<byte, Dictionary<PropertyNode, int>> _uniqueElements = new Dictionary<byte, Dictionary<PropertyNode, int>>();

        /// <summary>
		/// Initializes a new instance of the <see cref="BinaryFormatWriter"/> class.
		/// </summary>
		internal BinaryFormatWriter()
        {
        }

        /// <summary>
		/// Formats a node index based on the size.
		/// </summary>
		/// <param name="index">The index.</param>
		/// <param name="nodeIndexSize">The node index size.</param>
		/// <returns>The formated idx.</returns>
		private static byte[] FormatIdx(int index, byte nodeIndexSize)
        {
            switch (nodeIndexSize) {
                case 1: {
                    return new[] { (byte) index };
                }
                case 2: {
                    return EndianBitConverter.BigEndian.GetBytes((short) index);
                }
                case 4: {
                    return EndianBitConverter.BigEndian.GetBytes(index);
                }
                default: {
                    throw new PlistFormatException("Invalid node index size");
                }
            }
        }

        private static int GetNodeCount(PropertyNode node)
        {
            if (node == null) {
                throw new ArgumentNullException(nameof(node));
            }

            // Special case: array
            if (node is ArrayNode array) {
                int count = 1;
                foreach (PropertyNode subNode in array) {
                    count += GetNodeCount(subNode);
                }
                return count;
            }

            // Special case: dictionary
            if (node is DictionaryNode dictionary) {
                int count = 1;
                foreach (PropertyNode subNode in dictionary.Values) {
                    count += GetNodeCount(subNode);
                }
                count += dictionary.Keys.Count;
                return count;
            }

            // Normal case
            return 1;
        }

        /// <summary>
		/// Writers a <see cref="PropertyNode"/> to the current stream position
		/// </summary>
		/// <param name="stream">The stream.</param>
		/// <param name="nodeIndexSize">The node index size.</param>
		/// <param name="offsets">Node offsets.</param>
		/// <param name="node">The plist node.</param>
		/// <returns>The Idx of the written node</returns>
		internal int WriteInternal(Stream stream, byte nodeIndexSize, List<int> offsets, PropertyNode node)
        {
            int elementIdx = offsets.Count;
            if (node.IsBinaryUnique && node is IEquatable<PropertyNode>) {
                if (!_uniqueElements.TryGetValue(node.BinaryTag, out Dictionary<PropertyNode, int>? value)) {
                    value = new Dictionary<PropertyNode, int>();
                    _uniqueElements.Add(node.BinaryTag, value);
                }
                if (!value.TryGetValue(node, out int retValue)) {
                    value[node] = elementIdx;
                }
                else {
                    if (node is BooleanNode) {
                        elementIdx = retValue;
                    }
                    else {
                        return retValue;
                    }
                }
            }

            int offset = (int) stream.Position;
            offsets.Add(offset);
            int len = node.BinaryLength;
            byte typeCode = (byte) ((node.BinaryTag & 0xF0) | (len < 0x0F ? len : 0x0F));
            stream.WriteByte(typeCode);
            if (len >= 0x0F) {
                PropertyNode extLen = NodeFactory.CreateLengthElement(len);
                byte binaryTag = (byte) ((extLen.BinaryTag & 0xF0) | extLen.BinaryLength);
                stream.WriteByte(binaryTag);
                extLen.WriteBinary(stream);
            }

            if (node is ArrayNode arrayNode) {
                WriteInternal(stream, nodeIndexSize, offsets, arrayNode);
                return elementIdx;
            }

            if (node is DictionaryNode dictionaryNode) {
                WriteInternal(stream, nodeIndexSize, offsets, dictionaryNode);
                return elementIdx;
            }

            node.WriteBinary(stream);
            return elementIdx;
        }

        private void WriteInternal(Stream stream, byte nodeIndexSize, List<int> offsets, ArrayNode array)
        {
            byte[] nodes = new byte[nodeIndexSize * array.Count];
            long streamPos = stream.Position;

            stream.Write(nodes, 0, nodes.Length);
            for (int i = 0; i < array.Count; i++) {
                int elementIdx = WriteInternal(stream, nodeIndexSize, offsets, array[i]);
                FormatIdx(elementIdx, nodeIndexSize).CopyTo(nodes, nodeIndexSize * i);
            }

            stream.Seek(streamPos, SeekOrigin.Begin);
            stream.Write(nodes, 0, nodes.Length);
            stream.Seek(0, SeekOrigin.End);
        }

        private void WriteInternal(Stream stream, byte nodeIndexSize, List<int> offsets, DictionaryNode dictionary)
        {
            byte[] keys = new byte[nodeIndexSize * dictionary.Count];
            byte[] values = new byte[nodeIndexSize * dictionary.Count];
            long streamPos = stream.Position;
            stream.Write(keys, 0, keys.Length);
            stream.Write(values, 0, values.Length);

            KeyValuePair<string, PropertyNode>[] elems = dictionary.ToArray();

            for (int i = 0; i < dictionary.Count; i++) {
                int elementIdx = WriteInternal(stream, nodeIndexSize, offsets, NodeFactory.CreateKeyElement(elems[i].Key));
                FormatIdx(elementIdx, nodeIndexSize).CopyTo(keys, nodeIndexSize * i);
            }
            for (int i = 0; i < dictionary.Count; i++) {
                int elementIdx = WriteInternal(stream, nodeIndexSize, offsets, elems[i].Value);
                FormatIdx(elementIdx, nodeIndexSize).CopyTo(values, nodeIndexSize * i);
            }

            stream.Seek(streamPos, SeekOrigin.Begin);
            stream.Write(keys, 0, keys.Length);
            stream.Write(values, 0, values.Length);
            stream.Seek(0, SeekOrigin.End);
        }

        /// <summary>
		/// Writers a <see cref="PropertyNode"/> to the specified stream.
		/// </summary>
		/// <param name="stream">The stream.</param>
		/// <param name="node">The plist node.</param>
		public void Write(Stream stream, PropertyNode node)
        {
            stream.Write(_header, 0, _header.Length);

            var offsets = new List<int>();
            int nodeCount = GetNodeCount(node);

            byte nodeIndexSize;
            if (nodeCount <= byte.MaxValue) {
                nodeIndexSize = sizeof(byte);
            }
            else if (nodeCount <= short.MaxValue) {
                nodeIndexSize = sizeof(short);
            }
            else {
                nodeIndexSize = sizeof(int);
            }

            int topOffestIdx = WriteInternal(stream, nodeIndexSize, offsets, node);
            nodeCount = offsets.Count;

            int offsetTableOffset = (int) stream.Position;

            byte offsetSize;
            if (offsetTableOffset <= byte.MaxValue) {
                offsetSize = sizeof(byte);
            }
            else if (offsetTableOffset <= short.MaxValue) {
                offsetSize = sizeof(short);
            }
            else {
                offsetSize = sizeof(int);
            }

            for (int i = 0; i < offsets.Count; i++) {
                byte[]? buf = null;
                switch (offsetSize) {
                    case 1: {
                        buf = new[] { (byte) offsets[i] };
                        break;
                    }
                    case 2: {
                        buf = EndianBitConverter.BigEndian.GetBytes((short) offsets[i]);
                        break;
                    }
                    case 4: {
                        buf = EndianBitConverter.BigEndian.GetBytes(offsets[i]);
                        break;
                    }
                }
                if (buf != null) {
                    stream.Write(buf, 0, buf.Length);
                }
            }

            byte[] header = new byte[32];
            header[6] = offsetSize;
            header[7] = nodeIndexSize;

            EndianBitConverter.BigEndian.GetBytes(nodeCount).CopyTo(header, 12);
            EndianBitConverter.BigEndian.GetBytes(topOffestIdx).CopyTo(header, 20);
            EndianBitConverter.BigEndian.GetBytes(offsetTableOffset).CopyTo(header, 28);

            stream.Write(header, 0, header.Length);
        }
    }
}

using Netimobiledevice.Exceptions;
using System;
using System.IO;

namespace Netimobiledevice.Plist
{
    /// <summary>
    /// Represents a byte[] value from a Plist
    /// </summary>
    internal sealed class DataNode : PropertyNode<byte[]>
    {
        /// <summary>
        /// Gets the length of this PList element.
        /// </summary>
        internal override int BinaryLength => Value.Length;
        internal override PlistType NodeType => PlistType.Data;

        /// <summary>
        /// Initializes a new instance of the <see cref="DataNode"/> class.
        /// </summary>
        public DataNode()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DataNode"/> class.
        /// </summary>
        /// <param name="value">The value of this element.</param>
        public DataNode(byte[] value)
        {
            Value = value;
        }

        /// <summary>
        /// Parses the specified value from a given string (encoded as Base64), read from Xml.
        /// </summary>
        /// <param name="data">The string whis is parsed.</param>
        internal override void Parse(string data)
        {
            Value = Convert.FromBase64String(data);
        }

        /// <summary>
        /// Reads this element binary from the reader.
        /// </summary>
        internal override void ReadBinary(Stream stream, int nodeLength)
        {
            Value = new byte[nodeLength];
            if (stream.Read(Value, 0, Value.Length) != Value.Length) {
                throw new PlistFormatException();
            }
        }

        /// <summary>
        /// Gets the XML string representation of the Value.
        /// </summary>
        /// <returns>
        /// The XML string representation of the Value (encoded as Base64).
        /// </returns>
        internal override string ToXmlString()
        {
            return Convert.ToBase64String(Value);
        }

        /// <summary>
        /// Writes this element binary to the writer.
        /// </summary>
        internal override void WriteBinary(Stream stream)
        {
            stream.Write(Value, 0, Value.Length);
        }
    }
}

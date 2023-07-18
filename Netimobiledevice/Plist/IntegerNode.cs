using Netimobiledevice.EndianBitConversion;
using Netimobiledevice.Exceptions;
using System;
using System.Globalization;
using System.IO;

namespace Netimobiledevice.Plist
{
    /// <summary>
    /// Represents an integer value from a plist
    /// </summary>
    public sealed class IntegerNode : PropertyNode<ulong>
    {
        /// <summary>
        /// Gets the length of this PList element.
        /// </summary>
        /// <returns>The length of this PList element.</returns>
        /// <remarks>Provided for internal use only.</remarks>
        internal override int BinaryLength {
            get {
                if (Value >= byte.MinValue && Value <= byte.MaxValue) {
                    return 0;
                }
                if (Value >= ushort.MinValue && Value <= ushort.MaxValue) {
                    return 1;
                }
                if (Value >= uint.MinValue && Value <= uint.MaxValue) {
                    return 2;
                }
                if (Value >= ulong.MinValue && Value <= ulong.MaxValue) {
                    return 3;
                }
                return -1;
            }
        }
        internal override PlistType NodeType => PlistType.Integer;

        /// <summary>
        /// Gets or sets the value of this element.
        /// </summary>
        /// <value>The value of this element.</value>
        public override ulong Value { get; set; }

        public bool Unsigned { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="IntegerNode"/> class.
        /// </summary>
        public IntegerNode() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="IntegerNode"/> class.
        /// </summary>
        /// <param name="value">The value of this element.</param>
        public IntegerNode(int value)
        {
            Value = (ulong) value;
            Unsigned = false;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IntegerNode"/> class.
        /// </summary>
        /// <param name="value">The value of this element.</param>
        public IntegerNode(long value)
        {
            Value = (ulong) value;
            Unsigned = false;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IntegerNode"/> class.
        /// </summary>
        /// <param name="value">The value of this element.</param>
        public IntegerNode(short value)
        {
            Value = (ulong) value;
            Unsigned = false;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IntegerNode"/> class.
        /// </summary>
        /// <param name="value">The value of this element.</param>
        public IntegerNode(ulong value)
        {
            Value = value;
            Unsigned = true;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IntegerNode"/> class.
        /// </summary>
        /// <param name="value">The value of this element.</param>
        public IntegerNode(uint value)
        {
            Value = value;
            Unsigned = true;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IntegerNode"/> class.
        /// </summary>
        /// <param name="value">The value of this element.</param>
        public IntegerNode(ushort value)
        {
            Value = value;
            Unsigned = true;
        }

        /// <summary>
        /// Parses the specified value from a given string, read from Xml.
        /// </summary>
        /// <param name="data">The string whis is parsed.</param>
        internal override void Parse(string data)
        {
            Value = ulong.Parse(data, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Reads the binary stream.
        /// </summary>
        /// <param name="stream">Stream.</param>
        /// <param name="nodeLength">Node length.</param>
        internal override void ReadBinary(Stream stream, int nodeLength)
        {
            byte[] buf = new byte[1 << nodeLength];
            if (stream.Read(buf, 0, buf.Length) != buf.Length) {
                throw new PlistFormatException();
            }

            switch (nodeLength) {
                case 0: {
                    Value = buf[0];
                    break;
                }
                case 1: {
                    Value = EndianBitConverter.BigEndian.ToUInt16(buf, 0);
                    break;
                }
                case 2: {
                    Value = EndianBitConverter.BigEndian.ToUInt32(buf, 0);
                    break;
                }
                case 3: {
                    Value = EndianBitConverter.BigEndian.ToUInt64(buf, 0);
                    break;
                }
                default: {
                    throw new PlistFormatException("UInt > 64Bit");
                }
            }
        }

        /// <summary>
        /// Gets the XML string representation of the Value.
        /// </summary>
        /// <returns>
        /// The XML string representation of the Value.
        /// </returns>
        internal override string ToXmlString()
        {
            return Value.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Writes this element binary to the writer.
        /// </summary>
        internal override void WriteBinary(Stream stream)
        {
            byte[] buf;
            switch (BinaryLength) {
                case 0: {
                    buf = new[] { (byte) Value };
                    break;
                }
                case 1: {
                    buf = EndianBitConverter.BigEndian.GetBytes((ushort) Value);
                    break;
                }
                case 2: {
                    buf = EndianBitConverter.BigEndian.GetBytes((uint) Value);
                    break;
                }
                case 3: {
                    buf = EndianBitConverter.BigEndian.GetBytes(Value);
                    break;
                }
                default: {
                    throw new Exception($"Unexpected length: {BinaryLength}.");
                }
            }

            stream.Write(buf, 0, buf.Length);
        }

        /// <summary>
        /// Returns a <see cref="string"/> that represents the current PropertyNode
        /// </summary>
        /// <returns>A <see cref="string"/> that represents the current PropertyNode</returns>
        public override string ToString()
        {
            if (Unsigned) {
                return $"<{XmlTag}>: {Value}";
            }
            else {
                return $"<{XmlTag}>: {(long) Value}";
            }
        }
    }
}

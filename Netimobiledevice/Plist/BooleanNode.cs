using Netimobiledevice.Exceptions;
using System.IO;
using System.Xml;

namespace Netimobiledevice.Plist
{
    /// <summary>
	/// Represents a Boolean value from a PList
	/// </summary>
    internal sealed class BooleanNode : PropertyNode<bool>
    {
        internal override int BinaryLength => Value ? 9 : 8;
        /// <summary>
		/// Gets a value indicating whether this instance is written only once in binary mode.
		/// </summary>
		/// <value>
		/// true this instance is written only once in binary mode; otherwise, false.
		/// </value>
		internal override bool IsBinaryUnique => true;
        internal override PlistType NodeType => PlistType.Boolean;

        /// <summary>
        /// Initializes a new instance of the <see cref="BooleanNode"/> class.
        /// </summary>
        public BooleanNode()
        {
        }

        /// <summary>
		/// Initializes a new instance of the <see cref="BooleanNode"/> class.
		/// </summary>
		/// <param name="value">The Value of this element</param>
		public BooleanNode(bool value)
        {
            Value = value;
        }

        /// <summary>
		/// Parses the specified value from a given string, read from Xml.
		/// </summary>
		/// <param name="data">The string which is parsed.</param>
		internal override void Parse(string data)
        {
            Value = data == "true";
        }

        /// <summary>
		/// Reads this element binary from the reader.
		/// </summary>
		internal override void ReadBinary(Stream stream, int nodeLength)
        {
            if (nodeLength != 8 && nodeLength != 9) {
                throw new PlistFormatException();
            }
            Value = nodeLength == 9;
        }

        /// <summary>
		/// Generates an object from its XML representation.
		/// </summary>
		/// <param name="reader">The <see cref="XmlReader"/> stream from which the object is deserialized.</param>
		internal override void ReadXml(XmlReader reader)
        {
            Parse(reader.LocalName);
            reader.ReadStartElement();
        }

        /// <summary>
		/// Gets the XML string representation of the Value.
		/// </summary>
		/// <returns>
		/// The XML string representation of the Value.
		/// </returns>
		internal override string ToXmlString()
        {
            return Value ? "true" : "false";
        }

        /// <summary>
		/// Writes this element binary to the writer.
		/// </summary>
		internal override void WriteBinary(Stream stream)
        {
        }

        /// <summary>
		/// Converts an object into its XML representation.
		/// </summary>
		/// <param name="writer">The <see cref="XmlWriter"/> stream to which the object is serialized.</param>
		internal override void WriteXml(XmlWriter writer)
        {
            // Writing value as raw because Apple's parser expects no
            // space before the closing tag, and the XmlWrites inserts one
            // writer.WriteRaw($"<{ToXmlString()}/>");
            writer.WriteStartElement(ToXmlString());
            writer.WriteEndElement();
        }
    }
}

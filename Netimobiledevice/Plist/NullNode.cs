using Netimobiledevice.Exceptions;
using System.IO;
using System.Threading.Tasks;
using System.Xml;

namespace Netimobiledevice.Plist
{
    /// <summary>
    /// Represents a null element in a PList
    /// </summary>
    /// <remarks>Is skipped in Xml-Serialization</remarks>
    public sealed class NullNode : PropertyNode
    {
        internal override int BinaryLength => 0;
        /// <summary>
        /// Gets a value indicating whether this instance is written only once in binary mode.
        /// </summary>
        /// <value>
        /// true this instance is written only once in binary mode; otherwise, false.
        /// </value>
        internal override bool IsBinaryUnique => false;
        internal override PlistType NodeType => PlistType.Null;

        /// <summary>
        /// Reads this element binary from the reader.
        /// </summary>
        internal override void ReadBinary(Stream stream, int nodeLength)
        {
            if (nodeLength != 0x00) {
                throw new PlistFormatException();
            }
        }

        /// <summary>
        /// Generates an object from its XML representation.
        /// </summary>
        /// <param name="reader">The <see cref="XmlReader"/> stream from which the object is deserialized.</param>
        internal override void ReadXml(XmlReader reader)
        {
            reader.ReadStartElement(XmlTag);
        }

        internal override async Task ReadXmlAsync(XmlReader reader)
        {
            reader.ReadStartElement(XmlTag);
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
            writer.WriteStartElement(XmlTag);
            writer.WriteEndElement();
        }

        /// <summary>
        /// Returns a <see cref="System.String"/> that represents the NullNode
        /// </summary>
        /// <returns>A <see cref="System.String"/> that represents the NullNode</returns>
        public override string ToString()
        {
            return $"<{XmlTag}>";
        }
    }
}

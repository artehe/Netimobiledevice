using System.IO;
using System.Threading.Tasks;
using System.Xml;

namespace Netimobiledevice.Plist;

/// <summary>
/// Represents a fill element in a Plist
/// </summary>
/// <remarks>Is skipped in Xml-Serialization</remarks>
public sealed class FillNode : PropertyNode
{
    /// <summary>
    /// Gets the length of this PList node.
    /// </summary>
    internal override int BinaryLength => 0x0F;
    /// <summary>
    /// Gets a value indicating whether this instance is written only once in binary mode.
    /// </summary>
    /// <value>
    /// 	<c>true</c> this instance is written only once in binary mode; otherwise, <c>false</c>.
    /// </value>
    internal override bool IsBinaryUnique => false;
    internal override PlistType NodeType => PlistType.Fill;

    public FillNode()
    {
    }

    /// <summary>
    /// Reads this element binary from the reader.
    /// </summary>
    internal override void ReadBinary(Stream stream, int nodeLength)
    {
        if (nodeLength != 0x0F) {
            throw new PlistFormatException();
        }
    }

    internal override Task ReadBinaryAsync(Stream stream, int nodeLength)
    {
        if (nodeLength != 0x0F) {
            throw new PlistFormatException();
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Generates an object from its XML representation.
    /// </summary>
    /// <param name="reader">The <see cref="T:System.Xml.XmlReader"/> stream from which the object is deserialized.</param>
    internal override void ReadXml(XmlReader reader)
    {
        reader.ReadStartElement(XmlTag);
    }

    /// <summary>
    /// Generates an object from its XML representation.
    /// </summary>
    /// <param name="reader">The <see cref="T:System.Xml.XmlReader"/> stream from which the object is deserialized.</param>
    internal override async Task ReadXmlAsync(XmlReader reader)
    {
        await Task.Run(() => reader.ReadStartElement(XmlTag));
    }

    /// <summary>
    /// Writes this node binary to the writer.
    /// </summary>
    internal override void WriteBinary(Stream stream)
    {
        return;
    }

    internal override Task WriteBinaryAsync(Stream stream)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Converts an object into its XML representation.
    /// </summary>
    /// <param name="writer">The <see cref="T:System.Xml.XmlWriter"/> stream to which the object is serialized.</param>
    internal override void WriteXml(XmlWriter writer)
    {
        writer.WriteStartElement(XmlTag);
        writer.WriteEndElement();
    }

    internal override async Task WriteXmlAsync(XmlWriter writer)
    {
        await writer.WriteStartElementAsync(null, XmlTag, null);
        await writer.WriteEndElementAsync();
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

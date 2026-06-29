using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Netimobiledevice.Plist;

/// <summary>
/// Represents a string value from a PList 
/// </summary>
public sealed class StringNode : PropertyNode<string> {
    /// <summary>
    /// Gets the length of this PList element.
    /// </summary>
    /// <returns>The length of this PList element.</returns>
    internal override int BinaryLength => Value.Length;
    /// <summary>
    /// Gets or sets a value indicating whether this instance is UTF16.
    /// </summary>
    /// <value><c>true</c> if this instance is UTF16; otherwise, <c>false</c>.</value>
    internal bool IsUtf16 { get; set; }
    internal override PlistType NodeType => IsUtf16 ? PlistType.UString : PlistType.String;

    /// <summary>
    /// Gets or sets the value of this element.
    /// </summary>
    /// <value>The value of this element.</value>
    public sealed override string Value {
        get => _value;
        protected set {
            // Detect Encoding
            IsUtf16 = value.Any(c => c > 0x7F);
            _value = value;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StringNode"/> class.
    /// </summary>
    public StringNode() : base(string.Empty) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="StringNode"/> class.
    /// </summary>
    /// <param name="value">The value</param>
    public StringNode(string value) : base(value) {
        Value = value;
    }

    /// <summary>
    /// Parses the specified value from a given string, read from Xml.
    /// </summary>
    /// <param name="data">The string whis is parsed.</param>
    internal override void Parse(string data) {
        Value = data;
    }

    /// <summary>
    /// Reads this element binary from the reader.
    /// </summary>
    internal override void ReadBinary(Stream stream, int nodeLength) {
        byte[] buf = new byte[nodeLength * (IsUtf16 ? 2 : 1)];
        if (stream.Read(buf, 0, buf.Length) != buf.Length) {
            throw new PlistFormatException();
        }

        Encoding encoding = IsUtf16 ? Encoding.BigEndianUnicode : Encoding.UTF8;
        Value = encoding.GetString(buf);
    }

    internal override async Task ReadBinaryAsync(Stream stream, int nodeLength) {
        byte[] buf = new byte[nodeLength * (IsUtf16 ? 2 : 1)];
        if (await stream.ReadAsync(buf).ConfigureAwait(false) != buf.Length) {
            throw new PlistFormatException();
        }

        Encoding encoding = IsUtf16 ? Encoding.BigEndianUnicode : Encoding.UTF8;
        Value = encoding.GetString(buf);
    }

    /// <summary>
    /// Gets the XML string representation of the Value.
    /// </summary>
    /// <returns>
    /// The XML string representation of the Value.
    /// </returns>
    internal override string ToXmlString() {
        return Value;
    }

    /// <summary>
    /// Writes this element binary to the writer.
    /// </summary>
    internal override void WriteBinary(Stream stream) {
        Encoding enc = IsUtf16 ? Encoding.BigEndianUnicode : Encoding.UTF8;
        byte[] buf = enc.GetBytes(Value);
        stream.Write(buf);
    }

    internal override async Task WriteBinaryAsync(Stream stream) {
        Encoding enc = IsUtf16 ? Encoding.BigEndianUnicode : Encoding.UTF8;
        byte[] buf = enc.GetBytes(Value);
        await stream.WriteAsync(buf).ConfigureAwait(false);
    }

    internal override void WriteXml(XmlWriter writer) {
        writer.WriteElementString("string", Value);
    }
}

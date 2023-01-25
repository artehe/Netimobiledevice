using Netimobiledevice.EndianBitConversion;
using Netimobiledevice.Exceptions;
using System.Globalization;

namespace Netimobiledevice.Plist;

/// <summary>
/// Represents an integer value from a plist
/// </summary>
internal sealed class IntegerNode : PropertyNode<long>
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
            if (Value >= short.MinValue && Value <= short.MaxValue) {
                return 1;
            }
            if (Value >= int.MinValue && Value <= int.MaxValue) {
                return 2;
            }
            if (Value >= long.MinValue && Value <= long.MaxValue) {
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
    public override long Value { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="IntegerNode"/> class.
    /// </summary>
    public IntegerNode() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="IntegerNode"/> class.
    /// </summary>
    /// <param name="value">The value of this element.</param>
    public IntegerNode(long value)
    {
        Value = value;
    }

    /// <summary>
    /// Parses the specified value from a given string, read from Xml.
    /// </summary>
    /// <param name="data">The string whis is parsed.</param>
    internal override void Parse(string data)
    {
        Value = long.Parse(data, CultureInfo.InvariantCulture);
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
                Value = EndianBitConverter.BigEndian.ToInt16(buf, 0);
                break;
            }
            case 2: {
                Value = EndianBitConverter.BigEndian.ToInt32(buf, 0);
                break;
            }
            case 3: {
                Value = EndianBitConverter.BigEndian.ToInt64(buf, 0);
                break;
            }
            default: {
                throw new PlistFormatException("Int > 64Bit");
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
                buf = EndianBitConverter.BigEndian.GetBytes((short) Value);
                break;
            }
            case 2: {
                buf = EndianBitConverter.BigEndian.GetBytes((int) Value);
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
}

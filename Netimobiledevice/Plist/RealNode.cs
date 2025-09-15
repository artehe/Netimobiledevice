using Netimobiledevice.EndianBitConversion;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace Netimobiledevice.Plist;

/// <summary>
/// Represents a double value from a Plist
/// </summary>
public sealed class RealNode : PropertyNode<double>
{
    internal override int BinaryLength => 3;
    internal override PlistType NodeType => PlistType.Real;

    /// <summary>
    /// Initializes a new instance of the <see cref="RealNode"/> class.
    /// </summary>
    public RealNode() : base(0) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="RealNode"/> class.
    /// </summary>
    /// <param name="value">The value of this element.</param>
    public RealNode(double value) : base(value) { }

    /// <summary>
    /// Parses the specified value from a given string, read from Xml.
    /// </summary>
    /// <param name="data">The string whis is parsed.</param>
    internal override void Parse(string data)
    {
        Value = double.Parse(data, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Reads this element binary from the reader.
    /// </summary>
    internal override void ReadBinary(Stream stream, int nodeLength)
    {
        byte[] buf = new byte[1 << nodeLength];
        if (stream.Read(buf, 0, buf.Length) != buf.Length) {
            throw new PlistFormatException();
        }

        switch (nodeLength) {
            case 0: {
                throw new PlistFormatException("Real < 32Bit");
            }
            case 1: {
                throw new PlistFormatException("Real < 32Bit");
            }
            case 2: {
                Value = EndianBitConverter.BigEndian.ToSingle(buf, 0);
                break;
            }
            case 3: {
                Value = EndianBitConverter.BigEndian.ToDouble(buf, 0);
                break;
            }
            default: {
                throw new PlistFormatException("Real > 64Bit");
            }
        }
    }

    internal override async Task ReadBinaryAsync(Stream stream, int nodeLength)
    {
        byte[] buf = new byte[1 << nodeLength];
        if (await stream.ReadAsync(buf).ConfigureAwait(false) != buf.Length) {
            throw new PlistFormatException();
        }

        switch (nodeLength) {
            case 0: {
                throw new PlistFormatException("Real < 32Bit");
            }
            case 1: {
                throw new PlistFormatException("Real < 32Bit");
            }
            case 2: {
                Value = EndianBitConverter.BigEndian.ToSingle(buf, 0);
                break;
            }
            case 3: {
                Value = EndianBitConverter.BigEndian.ToDouble(buf, 0);
                break;
            }
            default: {
                throw new PlistFormatException("Real > 64Bit");
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
        byte[] buf = EndianBitConverter.BigEndian.GetBytes(Value);
        stream.Write(buf, 0, buf.Length);
    }

    internal override async Task WriteBinaryAsync(Stream stream)
    {
        byte[] buf = EndianBitConverter.BigEndian.GetBytes(Value);
        await stream.WriteAsync(buf).ConfigureAwait(false);
    }
}

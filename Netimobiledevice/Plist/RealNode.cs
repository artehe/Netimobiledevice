using Netimobiledevice.EndianBitConversion;
using System;
using System.Buffers.Binary;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace Netimobiledevice.Plist;

/// <summary>
/// Represents a double value from a Plist
/// </summary>
public sealed class RealNode : PropertyNode<double> {
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

    private void Decode(ReadOnlySpan<byte> buffer, int nodeLength) {
        Value = nodeLength switch {
            < 2 => throw new PlistFormatException("Binary real values must be at least 32 bits."),
            2 => BinaryPrimitives.ReadSingleBigEndian(buffer),
            3 => BinaryPrimitives.ReadDoubleBigEndian(buffer),
            _ => throw new PlistFormatException("Binary real values larger than 64 bits are not supported.")
        };
    }

    /// <summary>
    /// Parses the specified value from a given string, read from Xml.
    /// </summary>
    /// <param name="data">The string whis is parsed.</param>
    internal override void Parse(string data) {
        Value = double.Parse(data, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Reads this element binary from the reader.
    /// </summary>
    internal override void ReadBinary(Stream stream, int nodeLength) {
        byte[] buf = new byte[1 << nodeLength];
        stream.ReadExactly(buf);
        Decode(buf, nodeLength);
    }

    internal override async Task ReadBinaryAsync(Stream stream, int nodeLength) {
        byte[] buf = new byte[1 << nodeLength];
        await stream.ReadExactlyAsync(buf).ConfigureAwait(false);
        Decode(buf, nodeLength);
    }

    /// <summary>
    /// Gets the XML string representation of the Value.
    /// </summary>
    /// <returns>
    /// The XML string representation of the Value.
    /// </returns>
    internal override string ToXmlString() {
        return Value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Writes this element binary to the writer.
    /// </summary>
    internal override void WriteBinary(Stream stream) {
        byte[] buf = EndianBitConverter.BigEndian.GetBytes(Value);
        stream.Write(buf, 0, buf.Length);
    }

    internal override async Task WriteBinaryAsync(Stream stream) {
        byte[] buf = EndianBitConverter.BigEndian.GetBytes(Value);
        await stream.WriteAsync(buf).ConfigureAwait(false);
    }
}

using Netimobiledevice.EndianBitConversion;
using System;
using System.Buffers.Binary;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace Netimobiledevice.Plist;

/// <summary>
/// Represents a DateTime Value from a PList
/// </summary>
public sealed class DateNode : PropertyNode<DateTime> {
    private static DateTime MacEpoch => new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    internal override int BinaryLength => 3;
    internal override PlistType NodeType => PlistType.Date;

    /// <summary>
    /// Initializes a new instance of the <see cref="DateNode"/> class.
    /// </summary>
    public DateNode() : base(MacEpoch) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="DateNode"/> class.
    /// </summary>
    /// <param name="value">The value of this element.</param>
    public DateNode(DateTime value) : base(value) { }

    private static double ReadBinaryInternal(ReadOnlySpan<byte> data, int nodeLength) => nodeLength switch {
        < 2 => throw new PlistFormatException("Date < 32Bit"),
        2 => BinaryPrimitives.ReadSingleBigEndian(data),
        3 => BinaryPrimitives.ReadDoubleBigEndian(data),
        _ => throw new PlistFormatException("Date > 64Bit"),
    };

    /// <summary>
    /// Parses the specified value from a given string, read from Xml.
    /// </summary>
    /// <param name="data">The string whis is parsed.</param>
    internal override void Parse(string data) {
        Value = DateTime.Parse(data, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
    }

    /// <summary>
    /// Reads this element binary from the reader.
    /// </summary>
    internal override void ReadBinary(Stream stream, int nodeLength) {
        byte[] buf = new byte[1 << nodeLength];
        stream.ReadExactly(buf);

        double ticks = ReadBinaryInternal(buf, nodeLength);
        Value = MacEpoch.AddSeconds(ticks);
    }

    internal override async Task ReadBinaryAsync(Stream stream, int nodeLength) {
        byte[] buf = new byte[1 << nodeLength];
        await stream.ReadExactlyAsync(buf).ConfigureAwait(false);

        double ticks = ReadBinaryInternal(buf, nodeLength);
        Value = MacEpoch.AddSeconds(ticks);
    }

    /// <summary>
    /// Gets the XML string representation of the Value.
    /// </summary>
    /// <returns>
    /// The XML string representation of the Value.
    /// </returns>
    internal override string ToXmlString() {
        return Value.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ssZ", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Writes this element binary to the writer.
    /// </summary>
    internal override void WriteBinary(Stream stream) {
        TimeSpan ts = Value.ToUniversalTime() - MacEpoch;
        byte[] buf = EndianBitConverter.BigEndian.GetBytes(ts.TotalSeconds);
        stream.Write(buf);
    }

    internal override async Task WriteBinaryAsync(Stream stream) {
        TimeSpan ts = Value.ToUniversalTime() - MacEpoch;
        byte[] buf = EndianBitConverter.BigEndian.GetBytes(ts.TotalSeconds);
        await stream.WriteAsync(buf).ConfigureAwait(false);
    }
}

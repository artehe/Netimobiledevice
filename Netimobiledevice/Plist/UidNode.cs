using Netimobiledevice.EndianBitConversion;
using System;
using System.Buffers.Binary;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Xml;

namespace Netimobiledevice.Plist;

/// <summary>
/// Represents a UID value from a Plist
/// </summary>
public sealed class UidNode : PropertyNode<ulong> {
    internal override int BinaryLength {
        get {
            if (Value <= byte.MaxValue) {
                return 0;
            }
            if (Value <= ushort.MaxValue) {
                return 1;
            }
            return Value <= uint.MaxValue ? 2 : 3;
        }
    }
    internal override PlistType NodeType => PlistType.Uid;

    /// <summary>
    /// Create a new UID node.
    /// </summary>
    public UidNode() : base(0) { }

    /// <summary>
    ///	Create a new UID node.
    /// </summary>
    /// <param name="value"></param>
    public UidNode(ulong value) : base(value) { }

    private void ReadInternal(ReadOnlySpan<byte> data, int nodeLength) {
        switch (nodeLength) {
            case 0: {
                Value = data[0];
                break;
            }
            case 1: {
                Value = BinaryPrimitives.ReadUInt16BigEndian(data);
                break;
            }
            case 2: {
                Value = BinaryPrimitives.ReadUInt32BigEndian(data);
                break;
            }
            case 3: {
                Value = BinaryPrimitives.ReadUInt64BigEndian(data);
                break;
            }
            default: {
                throw new PlistFormatException("Int > 64Bit");
            }
        }
    }

    private byte[] WriteInternal() => BinaryLength switch {
        0 => [(byte) Value],
        1 => EndianBitConverter.BigEndian.GetBytes((ushort) Value),
        2 => EndianBitConverter.BigEndian.GetBytes((uint) Value),
        3 => EndianBitConverter.BigEndian.GetBytes(Value),
        _ => throw new PlistException($"Unexpected length: {BinaryLength}."),
    };

    internal override void Parse(string data) => throw new NotSupportedException("UID nodes cannot be parsed from XML plist format.");

    internal override void ReadBinary(Stream stream, int nodeLength) {
        byte[] buf = new byte[1 << nodeLength];
        stream.ReadExactly(buf);
        ReadInternal(buf, nodeLength);
    }

    internal override async Task ReadBinaryAsync(Stream stream, int nodeLength) {
        byte[] buf = new byte[1 << nodeLength];
        await stream.ReadExactlyAsync(buf).ConfigureAwait(false);
        ReadInternal(buf, nodeLength);
    }

    internal override string ToXmlString() {
        return Value.ToString(CultureInfo.InvariantCulture);
    }

    internal override void WriteBinary(Stream stream) {
        byte[] buf = WriteInternal();
        stream.Write(buf, 0, buf.Length);
    }

    internal override async Task WriteBinaryAsync(Stream stream) {
        byte[] buf = WriteInternal();
        await stream.WriteAsync(buf).ConfigureAwait(false);
    }

    internal override void WriteXml(XmlWriter writer) {
        writer.WriteStartElement("dict");
        writer.WriteElementString("key", "CF$UID");
        writer.WriteElementString("integer", Value.ToString(CultureInfo.InvariantCulture));
        writer.WriteEndElement();
    }
}

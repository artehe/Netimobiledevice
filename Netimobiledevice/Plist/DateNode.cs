using Netimobiledevice.EndianBitConversion;
using System;
using System.Globalization;
using System.IO;

namespace Netimobiledevice.Plist
{
    /// <summary>
    /// Represents a DateTime Value from a PList
    /// </summary>
    public sealed class DateNode : PropertyNode<DateTime>
    {
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

        /// <summary>
        /// Parses the specified value from a given string, read from Xml.
        /// </summary>
        /// <param name="data">The string whis is parsed.</param>
        internal override void Parse(string data)
        {
            Value = DateTime.Parse(data, CultureInfo.InvariantCulture);
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

            double ticks;
            switch (nodeLength) {
                case 0: {
                    throw new PlistFormatException("Date < 32Bit");
                }
                case 1: {
                    throw new PlistFormatException("Date < 32Bit");
                }
                case 2: {
                    ticks = EndianBitConverter.BigEndian.ToSingle(buf, 0);
                    break;
                }
                case 3: {
                    ticks = EndianBitConverter.BigEndian.ToDouble(buf, 0);
                    break;
                }
                default: {
                    throw new PlistFormatException("Date > 64Bit");
                }
            }

            Value = MacEpoch.AddSeconds(ticks);
        }

        /// <summary>
        /// Gets the XML string representation of the Value.
        /// </summary>
        /// <returns>
        /// The XML string representation of the Value.
        /// </returns>
        internal override string ToXmlString()
        {
            return Value.ToUniversalTime().ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss.ffffffZ");
        }

        /// <summary>
        /// Writes this element binary to the writer.
        /// </summary>
        internal override void WriteBinary(Stream stream)
        {
            TimeSpan ts = Value - MacEpoch;
            byte[] buf = EndianBitConverter.BigEndian.GetBytes(ts.TotalSeconds);
            stream.Write(buf, 0, buf.Length);
        }
    }
}

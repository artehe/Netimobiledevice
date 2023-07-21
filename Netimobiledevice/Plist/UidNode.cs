using Netimobiledevice.EndianBitConversion;
using Netimobiledevice.Exceptions;
using System;
using System.IO;

namespace Netimobiledevice.Plist
{
    /// <summary>
    /// Represents a UID value from a Plist
    /// </summary>
    public sealed class UidNode : PropertyNode<ulong>
    {
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
		/// Gets or sets the value of this element.
		/// </summary>
		/// <value>The value of this element.</value>
		public sealed override ulong Value { get; set; }


        /// <summary>
        /// Create a new UID node.
        /// </summary>
        public UidNode() : base(0) { }

        /// <summary>
		///	Create a new UID node.
		/// </summary>
		/// <param name="value"></param>
		public UidNode(ulong value) : base(value) { }

        internal override void Parse(string data)
        {
            throw new NotImplementedException();
        }

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
                    Value = EndianBitConverter.BigEndian.ToUInt16(buf, 0);
                    break;
                }
                case 2: {
                    Value = EndianBitConverter.BigEndian.ToUInt32(buf, 0);
                    break;
                }
                case 3: {
                    Value = EndianBitConverter.BigEndian.ToUInt64(buf, 0);
                    break;
                }
                default: {
                    throw new PlistFormatException("Int > 64Bit");
                }
            }
        }

        internal override string ToXmlString()
        {
            return $"<dict><key>CF$UID</key><integer>{Value}</integer></dict>";
        }

        internal override void WriteBinary(Stream stream)
        {
            byte[] buf;
            switch (BinaryLength) {
                case 0: {
                    buf = new[] { (byte) Value };
                    break;
                }
                case 1: {
                    buf = EndianBitConverter.BigEndian.GetBytes((ushort) Value);
                    break;
                }
                case 2: {
                    buf = EndianBitConverter.BigEndian.GetBytes((uint) Value);
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
}

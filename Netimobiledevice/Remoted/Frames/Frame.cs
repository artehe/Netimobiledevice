using Netimobiledevice.Exceptions;
using Netimobiledevice.Extentions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Netimobiledevice.Remoted.Frames
{
    internal abstract class Frame : IFrame
    {
        private uint? _payloadLength;

        public virtual byte Flags { get; protected set; }
        public abstract FrameType Type { get; }
        public virtual IEnumerable<byte> Payload { get; } = Array.Empty<byte>();
        public virtual uint StreamIdentifier { get; set; }

        public bool IsEndStream => (Type == FrameType.Data || Type == FrameType.Headers) && (Flags & 0x1) == 0x1;
        public uint Length => PayloadLength;
        public uint PayloadLength {
            get {
                if (!_payloadLength.HasValue) {
                    _payloadLength = (uint) Payload.Count();
                }
                return _payloadLength.Value;
            }
        }

        protected Frame()
        {
            Flags = 0;
        }

        public static Frame Create(FrameType frameType)
        {
            return frameType switch {
                FrameType.Data => new DataFrame(),
                FrameType.Headers => new HeadersFrame(),
                FrameType.Priority => new PriorityFrame(),
                FrameType.RstStream => new RstStreamFrame(),
                FrameType.Settings => new SettingsFrame(),
                FrameType.PushPromise => new PushPromiseFrame(),
                FrameType.Ping => new PingFrame(),
                FrameType.GoAway => new GoAwayFrame(),
                FrameType.WindowUpdate => new WindowUpdateFrame(),
                FrameType.Continuation => new ContinuationFrame(),
                _ => throw new NetimobiledeviceException("Unknown Frame type found"),
            };
        }

        public static Frame Create(byte frameType) => Create((FrameType) frameType);

        private static byte[] To24BitInt(uint original)
        {
            byte[] b = BitConverter.GetBytes(original);
            return [b[0], b[1], b[2]];
        }

        protected static byte ClearBit(byte target, int bitIndex)
        {
            int x = Convert.ToInt32(target);
            x &= ~(1 << bitIndex);
            return Convert.ToByte(x);
        }

        protected static uint ConvertFromUInt31(byte[] data)
        {
            if (data.Length != 4) {
                return 0;
            }
            data[3] = ClearBit(data[3], 7);
            return BitConverter.ToUInt32(data, 0);
        }

        protected static byte[] ConvertToUInt31(uint original)
        {
            // 1 Bit reserved as unset (0) so let's take the first bit of the next 32 bits and unset it
            byte[] data = BitConverter.GetBytes(original);
            data[3] = ClearBit(data[3], 7);
            return data;
        }

        internal void Parse(byte[] data)
        {
            var frameHeader = ParseFrameHeader(data);

            this.StreamIdentifier = frameHeader.StreamIdentifier;
            this.Flags = frameHeader.Flags;

            // Isolate the payload data
            byte[] payloadData = new byte[frameHeader.Length];
            Array.Copy(data, 9, payloadData, 0, frameHeader.Length);

            ParsePayload(payloadData, frameHeader);
        }

        public IEnumerable<byte> ToBytes()
        {
            List<byte> data = new List<byte>();

            // Copy Frame Length
            byte[] frameLength = To24BitInt(Length);
            data.AddRange(frameLength.EnsureBigEndian());

            // Copy Type
            data.Add((byte) Type);

            // Copy Flags
            data.Add(Flags);

            // 1 Bit reserved as unset (0) so let's take the first bit of the next 32 bits and unset it
            byte[] streamId = ConvertToUInt31(StreamIdentifier);
            data.AddRange(streamId.EnsureBigEndian());

            byte[] payloadData = Payload.ToArray();
            // Now the payload
            data.AddRange(payloadData);

            return data;
        }

        public static FrameHeader ParseFrameHeader(byte[] data)
        {
            if (data.Length < 9) {
                throw new InvalidDataException("data[] is missing frame header");
            }

            // Find out the frame length
            // which is a 24 bit uint, so we need to convert this as c# uint is 32 bit
            byte[] flen = [0x0, data[0], data[1], data[2]];

            uint frameLength = BitConverter.ToUInt32(flen.EnsureBigEndian(), 0);

            // If we are expecting a payload that's bigger than what's in our buffer
            // we should keep reading from the stream
            if (frameLength - 9 <= 0) {
                throw new InvalidDataException("frameLength smaller than amount of data");
            }

            byte frameType = data[3]; // 4th byte in frame header is TYPE
            byte frameFlags = data[4]; // 5th byte is FLAGS

            // we need to turn the stream id into a uint
            byte[] frameStreamIdData = new byte[4];
            Array.Copy(data, 5, frameStreamIdData, 0, 4);

            var frameHeader = new FrameHeader {
                Length = frameLength,
                Type = frameType,
                Flags = frameFlags,
                StreamIdentifier = ConvertFromUInt31(frameStreamIdData.EnsureBigEndian())
            };

            return frameHeader;
        }

        public abstract void ParsePayload(byte[] payloadData, FrameHeader frameHeader);

        public override string ToString() => $"[Frame: {Type.ToString().ToUpperInvariant()}, Id={StreamIdentifier}, Flags={Flags}, PayloadLength={PayloadLength}, IsEndStream={IsEndStream}]";
    }
}

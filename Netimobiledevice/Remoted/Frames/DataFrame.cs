using System;
using System.Collections.Generic;

namespace Netimobiledevice.Remoted.Frames
{
    internal class DataFrame : Frame
    {
        private uint _padLength = 0;

        public byte[] Data { get; set; }
        public bool EndStream { get; set; }
        public bool Padded { get; set; }

        public override byte Flags {
            get {
                byte endStream = EndStream ? (byte) 0x1 : (byte) 0x0;
                byte padded = Padded ? (byte) 0x8 : (byte) 0x0;
                return (byte) (endStream | padded);
            }
        }
        public override FrameType Type => FrameType.Data;
        public uint PadLength {
            get => _padLength;
            set {
                if (value > 255) {
                    throw new ArgumentOutOfRangeException("value", "Must be less than or equal to 255");
                }
                _padLength = value;
            }
        }

        public DataFrame() : base()
        {
        }

        public DataFrame(uint streamIdentifier) : base()
        {
            StreamIdentifier = streamIdentifier;
        }

        public override IEnumerable<byte> Payload {
            get {
                var data = new List<byte>();

                // Add the padding length - optional
                if (Padded && _padLength > 0) {
                    data.Add((byte) _padLength);
                }

                // Add the frame data
                if (Data != null) {
                    data.AddRange(Data);
                }

                // Add our padding
                for (int i = 0; i < _padLength; i++) {
                    data.Add(0x0);
                }
                return data;
            }
        }

        public override void ParsePayload(byte[] payloadData, FrameHeader frameHeader)
        {
            EndStream = (frameHeader.Flags & 0x1) == 0x1;
            Padded = (frameHeader.Flags & 0x8) == 0x8;

            int index = 0;

            if (Padded) {
                _padLength = payloadData[index];
                index++;
            }

            // Data will be length of total payload - pad length value - the actual padding
            Data = new byte[payloadData.Length - (index + _padLength)];
            Array.Copy(payloadData, index, Data, 0, Data.Length);
        }

        public override string ToString() => $"[Frame: DATA, Id={StreamIdentifier}, EndStream={IsEndStream}, Padded={Padded}, PadLength={PadLength}, PayloadLength={PayloadLength}]";
    }
}
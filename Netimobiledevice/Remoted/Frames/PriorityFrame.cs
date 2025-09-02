using Netimobiledevice.Extentions;
using System;
using System.Collections.Generic;

namespace Netimobiledevice.Remoted.Frames
{
    internal class PriorityFrame : Frame
    {
        private ushort _weight = 0;

        public override byte Flags => 0x0;
        public override IEnumerable<byte> Payload {
            get {
                var data = new List<byte>();

                // 1 Bit reserved as unset (0) so let's take the first bit of the next 32 bits and unset it
                data.AddRange(ConvertToUInt31(StreamDependency).EnsureBigEndian());
                data.Add((byte) Weight);

                return data.ToArray();
            }
        }
        public uint StreamDependency { get; set; } = 0;
        // type=0x1
        public override FrameType Type => FrameType.Priority;
        public ushort Weight {
            get => _weight;
            set {
                if (value > 255) {
                    throw new ArgumentOutOfRangeException("value", "Must be less than or equal to 255");
                }
                _weight = value;
            }
        }

        public PriorityFrame() : base()
        {
        }

        public PriorityFrame(uint streamIdentifier) : base()
        {
            StreamIdentifier = streamIdentifier;
        }

        public override void ParsePayload(byte[] payloadData, FrameHeader frameHeader)
        {
            // Get Dependency Stream Id
            // we need to turn the stream id into a uint
            var frameStreamIdData = new byte[4];
            Array.Copy(payloadData, 0, frameStreamIdData, 0, 4);
            StreamDependency = ConvertFromUInt31(frameStreamIdData.EnsureBigEndian());

            // Get the weight
            _weight = payloadData[4];
        }

        public override string ToString() => $"[Frame: PRIORITY, Id={StreamIdentifier}, StreamDependency={StreamDependency}, Weight={Weight}]";
    }
}
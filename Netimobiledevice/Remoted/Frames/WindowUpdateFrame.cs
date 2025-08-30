using Netimobiledevice.Extentions;
using System;
using System.Collections.Generic;

namespace Netimobiledevice.Remoted.Frames
{
    internal class WindowUpdateFrame : Frame
    {
        public uint WindowSizeIncrement { get; set; }

        public override FrameType Type => FrameType.WindowUpdate;
        public override IEnumerable<byte> Payload {
            get {
                var data = new List<byte>();
                // 1 Bit reserved as unset (0) so let's take the first bit of the next 32 bits and unset it
                data.AddRange(ConvertToUInt31(WindowSizeIncrement).EnsureBigEndian());
                return data;
            }
        }

        public override void ParsePayload(byte[] payloadData, FrameHeader frameHeader)
        {
            // we need to turn the stream id into a uint
            byte[] windowSizeIncrData = new byte[4];
            Array.Copy(payloadData, 0, windowSizeIncrData, 0, 4);
            WindowSizeIncrement = ConvertFromUInt31(windowSizeIncrData.EnsureBigEndian());
        }

        public override string ToString() => $"[Frame: WINDOW_UPDATE, Id={StreamIdentifier}, WindowSizeIncrement={WindowSizeIncrement}]";
    }
}
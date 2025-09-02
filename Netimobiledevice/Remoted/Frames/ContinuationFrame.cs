using System;
using System.Collections.Generic;

namespace Netimobiledevice.Remoted.Frames
{
    internal class ContinuationFrame : Frame, IFrameContainsHeaders
    {
        public bool EndHeaders { get; set; }
        public override byte Flags => EndHeaders ? (byte) 0x4 : (byte) 0x0;
        public byte[] HeaderBlockFragment { get; set; } = [];
        public override IEnumerable<byte> Payload => HeaderBlockFragment ?? new byte[0];
        // type=0x1
        public override FrameType Type => FrameType.Continuation;

        public ContinuationFrame() : base()
        {
        }

        public ContinuationFrame(uint streamIdentifier) : base()
        {
            StreamIdentifier = streamIdentifier;
        }

        public override void ParsePayload(byte[] payloadData, FrameHeader frameHeader)
        {
            EndHeaders = (frameHeader.Flags & 0x4) == 0x4;

            HeaderBlockFragment = new byte[payloadData.Length];
            payloadData.CopyTo(HeaderBlockFragment, 0);
        }

        public override string ToString()
        {
            return $"[Frame: CONTINUATION, Id={StreamIdentifier}, EndStream={IsEndStream}, EndHeaders={EndHeaders}, HeaderBlockFragmentLength={HeaderBlockFragment.Length}]";
        }

    }
}

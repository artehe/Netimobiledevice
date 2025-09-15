using System.Collections.Generic;

namespace Netimobiledevice.Remoted.Frames;

internal class RstStreamFrame : Frame
{
    public ErrorCode ErrorCode { get; set; }

    public override FrameType Type => FrameType.RstStream;

    public override byte Flags => 0x0;

    public override IEnumerable<byte> Payload {
        get {
            byte errorCode = (byte) ErrorCode;
            return new[] { errorCode };
        }
    }

    public RstStreamFrame() : base()
    {
    }

    public RstStreamFrame(uint streamIdentifier) : base()
    {
        StreamIdentifier = streamIdentifier;
    }


    public override void ParsePayload(byte[] payloadData, FrameHeader frameHeader)
    {
        if (payloadData != null && payloadData.Length > 0) {
            ErrorCode = (ErrorCode) payloadData[0];
        }
        else {
            ErrorCode = ErrorCode.NoError;
        }
    }

    public override string ToString() => $"[Frame: RST_STREAM, Id={StreamIdentifier}, ErrorCode={ErrorCode}]";
}

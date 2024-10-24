using System.Collections.Generic;

namespace Netimobiledevice.Remoted.Frames
{
    internal interface IFrame
    {
        uint Length { get; }
        FrameType Type { get; }
        byte Flags { get; }
        uint StreamIdentifier { get; set; }
        IEnumerable<byte> Payload { get; }
        uint PayloadLength { get; }
        IEnumerable<byte> ToBytes();
        bool IsEndStream { get; }
    }
}
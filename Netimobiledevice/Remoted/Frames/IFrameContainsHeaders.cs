namespace Netimobiledevice.Remoted.Frames;

internal interface IFrameContainsHeaders : IFrame
{
    byte[] HeaderBlockFragment { get; set; }
    bool EndHeaders { get; set; }
}

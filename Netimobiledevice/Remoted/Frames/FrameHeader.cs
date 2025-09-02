namespace Netimobiledevice.Remoted.Frames;

internal class FrameHeader
{
    public const int FrameHeaderLength = 9;

    public uint Length { get; set; }
    public byte Type { get; set; }
    public byte Flags { get; set; }
    public uint StreamIdentifier { get; set; }
}

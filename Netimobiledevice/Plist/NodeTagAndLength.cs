namespace Netimobiledevice.Plist;

internal class NodeTagAndLength(byte tag, int length)
{
    public byte Tag { get; private set; } = tag;
    public int Length { get; private set; } = length;
}

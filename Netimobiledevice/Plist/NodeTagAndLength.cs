namespace Netimobiledevice.Plist
{
    internal class NodeTagAndLength
    {
        public byte Tag { get; private set; }
        public int Length { get; private set; }

        public NodeTagAndLength(byte tag, int length)
        {
            Tag = tag;
            Length = length;
        }
    }
}

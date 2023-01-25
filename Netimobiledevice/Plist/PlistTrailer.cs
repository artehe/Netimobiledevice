namespace Netimobiledevice.Plist
{
    internal struct PlistTrailer
    {
        public byte[] Unused;
        public byte SortVersionl;
        public byte OffsetIntSize;
        public byte ObjectRefSize;
        public ulong NumObjects;
        public ulong TopObject;
        public ulong OffsetTableOffset;

        // From Apple docs
        //  uint8_t _unused[5];
        //	uint8_t _sortVersion;
        //	uint8_t _offsetIntSize;
        //	uint8_t _objectRefSize;
        //	uint64_t _numObjects;
        //	uint64_t _topObject;
        //	uint64_t _offsetTableOffset;
    }
}

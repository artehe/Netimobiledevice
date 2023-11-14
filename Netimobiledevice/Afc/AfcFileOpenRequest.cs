using Netimobiledevice.Extentions;
using System;
using System.Collections.Generic;

namespace Netimobiledevice.Afc
{
    internal class AfcFileOpenRequest
    {
        public AfcFileOpenMode Mode { get; }
        public CString Filename { get; }

        public AfcFileOpenRequest(AfcFileOpenMode mode, CString filename)
        {
            Mode = mode;
            Filename = filename;
        }

        public byte[] GetBytes()
        {
            List<byte> bytes = new List<byte>();
            bytes.AddRange(BitConverter.GetBytes((ulong) Mode));
            bytes.AddRange(Filename.Bytes);

            return bytes.ToArray();
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Netimobiledevice.Afc
{
    internal class AfcHeader
    {
        private const string MAGIC_STRING = "CFA6LPAA";

        public byte[] Magic { get; } = Encoding.UTF8.GetBytes(MAGIC_STRING);
        public ulong EntireLength { get; set; }
        public ulong Length { get; set; }
        public ulong PacketNumber { get; set; }
        public AfcOpCode Operation { get; set; }

        public static AfcHeader FromBytes(byte[] bytes)
        {
            using (MemoryStream ms = new MemoryStream(bytes)) {
                byte[] magicBytes = Encoding.UTF8.GetBytes(MAGIC_STRING);
                byte[] readMagicBytes = new byte[magicBytes.Length];
                ms.Read(readMagicBytes, 0, readMagicBytes.Length);
                for (int i = 0; i < magicBytes.Length; i++) {
                    if (magicBytes[i] != readMagicBytes[i]) {
                        throw new AfcException("Missmatch in magic bytes for afc header");
                    }
                }
            }

            AfcHeader afcHeader = new AfcHeader() {
                EntireLength = BitConverter.ToUInt64(bytes, 8),
                Length = BitConverter.ToUInt64(bytes, 16),
                PacketNumber = BitConverter.ToUInt64(bytes, 24),
                Operation = (AfcOpCode) BitConverter.ToUInt64(bytes, 32),
            };
            return afcHeader;
        }

        public byte[] GetBytes()
        {
            List<byte> bytes = new List<byte>();
            bytes.AddRange(Magic);
            bytes.AddRange(BitConverter.GetBytes(EntireLength));
            bytes.AddRange(BitConverter.GetBytes(Length));
            bytes.AddRange(BitConverter.GetBytes(PacketNumber));
            bytes.AddRange(BitConverter.GetBytes((ulong) Operation));

            return bytes.ToArray();
        }

        public static int GetSize()
        {
            int size = Encoding.UTF8.GetBytes(MAGIC_STRING).Length;
            size += sizeof(ulong) * 4;
            return size;
        }
    }
}

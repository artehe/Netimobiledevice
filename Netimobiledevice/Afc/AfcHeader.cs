using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Netimobiledevice.Afc
{
    internal class AfcHeader
    {
        public const string Magic = "CFA6LPAA";
        public ulong EntireLength;
        public ulong Length;
        public ulong PacketNumber;
        public AfcOpCode Operation;

        public static AfcHeader FromBytes(byte[] bytes)
        {
            using (MemoryStream ms = new MemoryStream(bytes)) {
                byte[] magicBytes = Encoding.UTF8.GetBytes(Magic);
                byte[] readMagicBytes = new byte[magicBytes.Length];
                ms.Read(readMagicBytes, 0, readMagicBytes.Length);
                for (int i = 0; i < magicBytes.Length; i++) {
                    if (magicBytes[i] != readMagicBytes[i]) {
                        throw new Exception("Missmatch in magic bytes for afc header");
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
            bytes.AddRange(Encoding.UTF8.GetBytes(Magic));
            bytes.AddRange(BitConverter.GetBytes(EntireLength));
            bytes.AddRange(BitConverter.GetBytes(Length));
            bytes.AddRange(BitConverter.GetBytes(PacketNumber));
            bytes.AddRange(BitConverter.GetBytes((ulong) Operation));

            return bytes.ToArray();
        }

        public static int GetSize()
        {
            int size = Encoding.UTF8.GetBytes(Magic).Length;
            size += sizeof(ulong);
            size += sizeof(ulong);
            size += sizeof(ulong);
            size += sizeof(ulong);
            return size;
        }
    }
}

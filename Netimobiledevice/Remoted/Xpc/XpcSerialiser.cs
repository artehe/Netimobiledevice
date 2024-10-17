using Netimobiledevice.Extentions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Netimobiledevice.Remoted.Xpc
{
    public static class XpcSerialiser
    {
        private static byte[] AlignData(byte[] data, int alignment)
        {
            // Make sure we align the string to the requested alignement number of bytes
            List<byte> alignmentData = [];
            int alignmentAmount = data.Length % alignment;
            for (int i = 0; i < alignmentAmount; i++) {
                alignmentData.Add(0x00);
            }
            return [.. data, .. alignmentData];
        }

        private static byte[] GetPrefixSizeFromData(byte[] data)
        {
            int length = BitConverter.ToInt32(data.Take(sizeof(int)).ToArray());
            if (data.Length - length - sizeof(uint) > 0) {
                throw new FormatException($"Expected {length} + 4 bytes but got {data.Length} bytes");
            }
            return data.Skip(sizeof(int)).Take(length).ToArray();
        }

        private static string DeserialiseAlignedString(byte[] data)
        {
            int zeroIndex = -1;
            for (int i = 0; i < data.Length; i++) {
                if (data[i] == 0) {
                    zeroIndex = i;
                    break;
                }
            }
            return Encoding.UTF8.GetString(data, 0, zeroIndex);
        }

        private static XpcDictionaryObject DeserialiseXpcDictionary(byte[] data)
        {
            XpcDictionaryObject dict = [];

            data = GetPrefixSizeFromData(data);

            int entryCount = BitConverter.ToInt32(data.Take(sizeof(int)).ToArray());
            data = data.Skip(sizeof(int)).ToArray();
            for (int i = 0; i < entryCount; i++) {
                string key = DeserialiseAlignedString(data);
                int size = SerialiseAlignedString(key).Length;
                data = data.Skip(size).ToArray();

                XpcObject xpcObject = Deserialise(data);
                size = Serialise(xpcObject).Length;
                data = data.Skip(size).ToArray();
            }
            return dict;
        }

        private static XpcInt64 DeserialiseXpcInt64(byte[] data)
        {
            long value = BitConverter.ToInt64(data, 0);
            return new XpcInt64(value);
        }

        private static byte[] SerialiseAlignedString(string str)
        {
            byte[] keyString = str.AsCString().GetBytes(Encoding.UTF8);
            byte[] alignedStr = AlignData(keyString, 4);
            return alignedStr;
        }

        private static byte[] SerialiseXpcDictionary(XpcDictionaryObject dict)
        {
            List<byte> entries = [];
            foreach (KeyValuePair<string, XpcObject> entry in dict) {
                entries.AddRange(SerialiseAlignedString(entry.Key));
                entries.AddRange(Serialise(entry.Value));
            }

            return [
                .. BitConverter.GetBytes(dict.Count),
                .. entries.ToArray()
            ];
        }

        private static byte[] SerialiseXpcInt64(XpcInt64 int64)
        {
            return BitConverter.GetBytes(int64.Data);
        }

        public static XpcObject Deserialise(byte[] data)
        {
            XpcMessageType type = (XpcMessageType) BitConverter.ToUInt32(data.Take(4).ToArray());
            data = data.Skip(4).ToArray();
            XpcObject xpcObject = type switch {
                XpcMessageType.Dictionary => DeserialiseXpcDictionary(data),
                XpcMessageType.Int64 => DeserialiseXpcInt64(data),
                _ => throw new InvalidOperationException($"Not supported XpcObject type {type}")
            };
            return xpcObject;
        }

        public static byte[] Serialise(XpcObject obj)
        {
            byte[] data = obj switch {
                XpcDictionaryObject => SerialiseXpcDictionary((XpcDictionaryObject) obj),
                XpcInt64 => SerialiseXpcInt64((XpcInt64) obj),
                _ => throw new InvalidOperationException($"Not supported XpcObject type {obj.Type}")
            };
            if (obj.IsPrefixed) {
                return [
                    .. BitConverter.GetBytes((uint) obj.Type),
                    .. BitConverter.GetBytes(data.Length),
                    .. data
                ];
            }
            return [
                    .. BitConverter.GetBytes((uint) obj.Type),
                    .. data
                ];
        }
    }
}

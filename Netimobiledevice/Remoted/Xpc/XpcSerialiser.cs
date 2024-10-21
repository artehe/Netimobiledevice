using System;
using System.Collections.Generic;
using System.Linq;

namespace Netimobiledevice.Remoted.Xpc
{
    public static class XpcSerialiser
    {
        public static byte[] AlignData(byte[] data, int alignment)
        {
            // Make sure we align the string to the requested alignement number of bytes
            List<byte> alignmentData = [];

            int alignmentAmount = data.Length % alignment;
            if (alignmentAmount == 0) {
                // If alignmentAmount is zero we can just return
                return data;
            }

            // We have the remainder so we need to invert this to work out the
            // required amount of padding
            alignmentAmount = (alignmentAmount - alignment) * -1;

            // Add the padding
            for (int i = 0; i < alignmentAmount; i++) {
                alignmentData.Add(0x00);
            }
            return [.. data, .. alignmentData];
        }

        public static XpcObject Deserialise(byte[] data)
        {
            XpcMessageType type = (XpcMessageType) BitConverter.ToUInt32(data.Take(4).ToArray());
            data = data.Skip(4).ToArray();
            XpcObject xpcObject = type switch {
                XpcMessageType.Array => XpcArray.Deserialise(data),
                XpcMessageType.Bool => XpcBool.Deserialise(data),
                XpcMessageType.Dictionary => XpcDictionaryObject.Deserialise(data),
                XpcMessageType.Double => XpcDouble.Deserialise(data),
                XpcMessageType.Int64 => XpcInt64.Deserialise(data),
                XpcMessageType.Null => XpcNull.Deserialise(data),
                XpcMessageType.String => XpcString.Deserialise(data),
                XpcMessageType.Uint64 => XpcUInt64.Deserialise(data),
                _ => throw new InvalidOperationException($"Not supported XpcObject type {type}")
            };
            return xpcObject;
        }

        public static byte[] Serialise(XpcObject obj)
        {
            byte[] data = obj.Serialise();

            if (obj.IsPrefixed) {
                data = [
                    .. BitConverter.GetBytes(data.Length),
                    .. data
                ];
            }

            if (obj.IsAligned) {
                data = AlignData(data, 4);
            }

            return [
                .. BitConverter.GetBytes((uint) obj.Type),
                .. data
            ];
        }
    }
}

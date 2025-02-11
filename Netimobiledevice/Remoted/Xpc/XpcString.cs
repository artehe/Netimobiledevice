using Netimobiledevice.Extentions;
using System.Text;

namespace Netimobiledevice.Remoted.Xpc
{
    public class XpcString(string? data) : XpcObject<string>(data?.TrimEnd('\0'))
    {
        public override bool IsAligned => true;

        public override bool IsPrefixed => true;

        public override XpcMessageType Type => XpcMessageType.String;

        public static XpcString Deserialise(byte[] data)
        {
            data = GetPrefixSizeFromData(data);
            return new XpcString(Encoding.UTF8.GetString(data));
        }

        public override byte[] Serialise()
        {
            string str = Data ?? string.Empty;
            return str.AsCString(Encoding.UTF8).GetBytes();
        }
    }
}

using System;
using System.Linq;

namespace Netimobiledevice.Remoted.Xpc
{
    public abstract class XpcObject
    {
        public abstract bool IsAligned { get; }

        public abstract bool IsPrefixed { get; }

        public abstract XpcMessageType Type { get; }

        public XpcObject() { }

        public abstract byte[] Serialise();

        protected static byte[] GetPrefixSizeFromData(byte[] data)
        {
            int length = BitConverter.ToInt32(data.Take(sizeof(int)).ToArray());
            return data.Skip(sizeof(int)).Take(length).ToArray();
        }
        public XpcDictionary AsXpcDictionary()
        {
            return (XpcDictionary) this;
        }
        public XpcString AsXpcString()
        {
            return (XpcString) this;
        }

        public XpcUuid AsXpcUuid()
        {
            return (XpcUuid) this;
        }
    }

    public abstract class XpcObject<T>(T? data) : XpcObject
    {
        public virtual T? Data { get; set; } = data;
    }
}

using System;
using System.Collections.Generic;

namespace Netimobiledevice.Remoted.Xpc
{
    public abstract class XpcObject
    {
        public abstract XpcMessageType Type { get; }

        private static XpcObject ParseXpcDictionary(Dictionary<string, object> dict)
        {
            XpcDictionaryObject xpcDict = new XpcDictionaryObject();
            foreach (KeyValuePair<string, object> entry in dict) {
                xpcDict.Add(entry.Key, Parse(entry.Value));
            }
            return xpcDict;
        }

        public static XpcObject Parse(object? payload)
        {
            if (payload is null) {
                return new XpcNullObject();
            }
            return payload switch {
                Dictionary<string, object> dict => ParseXpcDictionary(dict),
                _ => throw new InvalidOperationException($"Unrecognized type: {payload.GetType()}")
            };
        }

        public abstract byte[] Serialise();
    }

    public abstract class XpcObject<T> : XpcObject
    {
        public virtual T? Data { get; set; }
    }
}

using Netimobiledevice.Extentions;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;

namespace Netimobiledevice.Remoted.Xpc;

public class XpcDictionary : XpcObject, IDictionary<string, XpcObject>
{
    private readonly IDictionary<string, XpcObject> _dictionary = new Dictionary<string, XpcObject>();

    public XpcObject this[string key] {
        get => _dictionary[key];
        set => _dictionary[key] = value;
    }

    public override bool IsAligned => false;

    public override bool IsPrefixed => true;

    public override XpcMessageType Type => XpcMessageType.Dictionary;

    public ICollection<string> Keys => _dictionary.Keys;

    public ICollection<XpcObject> Values => _dictionary.Values;

    public int Count => _dictionary.Count;

    public bool IsReadOnly => false;

    public XpcDictionary() { }

    public XpcDictionary(IDictionary<string, XpcObject> data)
    {
        _dictionary = data;
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

    private static byte[] SerialiseAlignedString(string str)
    {
        byte[] stringBytes = str.AsCString(Encoding.UTF8).GetBytes();
        byte[] alignedStr = XpcSerialiser.AlignData(stringBytes, 4);
        return alignedStr;
    }

    public void Add(string key, XpcObject value)
    {
        _dictionary.Add(key, value);
    }

    public void Add(KeyValuePair<string, XpcObject> item)
    {
        _dictionary.Add(item);
    }

    public void Clear()
    {
        _dictionary.Clear();
    }

    public bool Contains(KeyValuePair<string, XpcObject> item)
    {
        return _dictionary.Contains(item);
    }

    public bool ContainsKey(string key)
    {
        return _dictionary.ContainsKey(key);
    }

    public void CopyTo(KeyValuePair<string, XpcObject>[] array, int arrayIndex)
    {
        _dictionary.CopyTo(array, arrayIndex);
    }

    public IEnumerator<KeyValuePair<string, XpcObject>> GetEnumerator()
    {
        return _dictionary.GetEnumerator();
    }

    public bool Remove(string key)
    {
        return _dictionary.Remove(key);
    }

    public bool Remove(KeyValuePair<string, XpcObject> item)
    {
        return _dictionary.Remove(item);
    }

    public bool TryGetValue(string key, [MaybeNullWhen(false)] out XpcObject value)
    {
        return _dictionary.TryGetValue(key, out value);
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
        return _dictionary.GetEnumerator();
    }

    public static XpcDictionary Deserialise(byte[] data)
    {
        XpcDictionary dict = [];

        data = GetPrefixSizeFromData(data);

        int entryCount = BitConverter.ToInt32(data.Take(sizeof(int)).ToArray());
        data = data.Skip(sizeof(int)).ToArray();
        for (int i = 0; i < entryCount; i++) {
            string key = DeserialiseAlignedString(data);
            int size = SerialiseAlignedString(key).Length;
            data = data.Skip(size).ToArray();

            XpcObject xpcObject = XpcSerialiser.Deserialise(data);
            size = XpcSerialiser.Serialise(xpcObject).Length;
            data = data.Skip(size).ToArray();

            dict.Add(key, xpcObject);
        }
        return dict;
    }

    public override byte[] Serialise()
    {
        List<byte> entries = [];
        foreach (KeyValuePair<string, XpcObject> entry in _dictionary) {
            entries.AddRange(SerialiseAlignedString(entry.Key));
            entries.AddRange(XpcSerialiser.Serialise(entry.Value));
        }

        return [
            .. BitConverter.GetBytes(Count),
            .. entries.ToArray()
        ];
    }
}

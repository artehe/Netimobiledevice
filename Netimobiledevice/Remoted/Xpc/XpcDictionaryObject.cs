using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Netimobiledevice.Remoted.Xpc
{
    public class XpcDictionaryObject : XpcObject, IDictionary<string, XpcObject>
    {
        private readonly IDictionary<string, XpcObject> _dictionary = new Dictionary<string, XpcObject>();

        public XpcObject this[string key] {
            get => _dictionary[key];
            set => _dictionary[key] = value;
        }

        public override XpcMessageType Type => XpcMessageType.Dictionary;

        public ICollection<string> Keys => _dictionary.Keys;

        public ICollection<XpcObject> Values => _dictionary.Values;

        public int Count => _dictionary.Count;

        public bool IsReadOnly => false;

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
    }
}
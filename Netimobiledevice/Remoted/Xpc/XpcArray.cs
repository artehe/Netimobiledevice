using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Netimobiledevice.Remoted.Xpc
{
    public class XpcArray : XpcObject, IList<XpcObject>
    {
        private readonly IList<XpcObject> _list = [];

        public XpcObject this[int index] {
            get => _list[index];
            set => _list[index] = value;
        }

        public override bool IsAligned => false;

        public override bool IsPrefixed => true;

        public override XpcMessageType Type => XpcMessageType.Array;

        public int Count => _list.Count;

        public bool IsReadOnly => false;

        public void Add(XpcObject item)
        {
            _list.Add(item);
        }

        public void Clear()
        {
            _list.Clear();
        }

        public bool Contains(XpcObject item)
        {
            return _list.Contains(item);
        }

        public void CopyTo(XpcObject[] array, int arrayIndex)
        {
            _list.CopyTo(array, arrayIndex);
        }

        public IEnumerator<XpcObject> GetEnumerator()
        {
            return _list.GetEnumerator();
        }

        public int IndexOf(XpcObject item)
        {
            return _list.IndexOf(item);
        }

        public void Insert(int index, XpcObject item)
        {
            _list.Insert(index, item);
        }

        public bool Remove(XpcObject item)
        {
            return _list.Remove(item);
        }

        public void RemoveAt(int index)
        {
            _list.RemoveAt(index);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _list.GetEnumerator();
        }

        public static XpcArray Deserialise(byte[] data)
        {
            XpcArray arr = [];

            data = GetPrefixSizeFromData(data);

            int entryCount = BitConverter.ToInt32(data.Take(sizeof(int)).ToArray());
            data = data.Skip(sizeof(int)).ToArray();
            for (int i = 0; i < entryCount; i++) {
                XpcObject xpcObject = Deserialise(data);
                int size = XpcSerialiser.Serialise(xpcObject).Length;
                data = data.Skip(size).ToArray();

                arr.Add(xpcObject);
            }
            return arr;
        }

        public override byte[] Serialise()
        {
            List<byte> entries = [];
            foreach (XpcObject entry in _list) {
                entries.AddRange(XpcSerialiser.Serialise(entry));
            }
            return [
                .. BitConverter.GetBytes(Count),
                .. entries.ToArray()
            ];
        }
    }
}

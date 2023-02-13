using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace Netimobiledevice.Plist
{
    internal sealed class ArrayNode : PropertyNode, IList<PropertyNode>
    {
        private readonly IList<PropertyNode> _list = new List<PropertyNode>();

        internal override int BinaryLength => _list.Count;
        /// <summary>
        /// Gets a value indicating whether this instance is written only once in binary mode.
        /// </summary>
        /// <value>
        /// true this instance is written only once in binary mode; otherwise, false.
        /// </value>
        internal override bool IsBinaryUnique => false;
        internal override PlistType NodeType => PlistType.Array;
        /// <summary>
        /// Gets the number of nodes in the array.
        /// </summary>
        /// <value>The total nodes in the array</value>
        public int Count => _list.Count;
        /// <summary>
        /// Gets a value indicating whether this instance is read only.
        /// </summary>
        /// <value>true if this instance is read only; otherwise, false.</value>
        public bool IsReadOnly => false;

        /// <summary>
        /// Gets or sets the <see cref="PListNet.PNode"/> at the specified index.
        /// </summary>
        /// <param name="index">Index.</param>
        public PropertyNode this[int index] {
            get => _list[index];
            set => _list[index] = value;
        }

        /// <summary>
        /// Gets the enumerator.
        /// </summary>
        /// <returns>The enumerator.</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return _list.GetEnumerator();
        }

        /// <summary>
        /// Reads this element binary from the reader.
        /// </summary>
        internal override void ReadBinary(Stream stream, int nodeLength)
        {
            throw new NotImplementedException("This type of node does not do it's own reading, refer to the binary reader.");
        }

        /// <summary>
        /// Generates an object from its XML representation.
        /// </summary>
        /// <param name="reader">The <see cref="XmlReader"/> stream from which the object is deserialized.</param>
        internal override void ReadXml(XmlReader reader)
        {
            bool wasEmpty = reader.IsEmptyElement;
            reader.Read();

            if (wasEmpty) {
                return;
            }

            // Make sure we are position at an element, skipping white space and such
            _ = reader.MoveToContent();

            while (reader.NodeType != XmlNodeType.EndElement) {
                var plelem = NodeFactory.Create(reader.LocalName);
                plelem.ReadXml(reader);

                Add(plelem);
                reader.MoveToContent();
            }

            reader.ReadEndElement();
        }

        internal override void WriteBinary(Stream stream)
        {
            throw new NotImplementedException("This type of node does not do it's own writing, refer to the binary writer.");
        }

        /// <summary>
        /// Converts an object into its XML representation.
        /// </summary>
        /// <param name="writer">The <see cref="XmlWriter"/> stream to which the object is serialized.</param>
        internal override void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement(XmlTag);
            for (int i = 0; i < Count; i++) {
                this[i].WriteXml(writer);
            }
            writer.WriteEndElement();
        }

        /// <summary>
        /// Add the specified item.
        /// </summary>
        /// <param name="item">Item.</param>
        public void Add(PropertyNode item)
        {
            _list.Add(item);
        }

        /// <summary>
        /// Clear this instance.
        /// </summary>
        public void Clear()
        {
            _list.Clear();
        }

        /// <summary>
        /// Determines whether the current collection contains a specific value.
        /// </summary>
        /// <param name="item">Item.</param>
        public bool Contains(PropertyNode item)
        {
            return _list.Contains(item);
        }

        /// <summary>
        /// Copies array.
        /// </summary>
        /// <param name="array">Array.</param>
        /// <param name="arrayIndex">Array index.</param>
        public void CopyTo(PropertyNode[] array, int arrayIndex)
        {
            _list.CopyTo(array, arrayIndex);
        }

        /// <summary>
        /// Gets the enumerator.
        /// </summary>
        /// <returns>The enumerator.</returns>
        public IEnumerator<PropertyNode> GetEnumerator()
        {
            return _list.GetEnumerator();
        }

        /// <summary>
        /// Determines the index of a specific item in the current instance.
        /// </summary>
        /// <returns>The index.</returns>
        /// <param name="item">Item.</param>
        public int IndexOf(PropertyNode item)
        {
            return _list.IndexOf(item);
        }

        /// <summary>
        /// Insert the specified item at index.
        /// </summary>
        /// <param name="index">Index.</param>
        /// <param name="item">Item.</param>
        public void Insert(int index, PropertyNode item)
        {
            _list.Insert(index, item);
        }

        /// <summary>
        /// Remove the specified item.
        /// </summary>
        /// <param name="item">Item.</param>
        public bool Remove(PropertyNode item)
        {
            return _list.Remove(item);
        }

        /// <summary>
        /// Removes the item at index.
        /// </summary>
        /// <param name="index">Index.</param>
        public void RemoveAt(int index)
        {
            _list.RemoveAt(index);
        }
    }
}

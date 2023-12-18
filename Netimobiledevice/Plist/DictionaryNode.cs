using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;
using System.Xml;

namespace Netimobiledevice.Plist
{
    /// <summary>
    /// Represents a Dictionary value from a plist.
    /// </summary>
    public sealed class DictionaryNode : PropertyNode, IDictionary<string, PropertyNode>
    {
        private readonly IDictionary<string, PropertyNode> _dictionary = new Dictionary<string, PropertyNode>();

        /// <summary>
        /// Gets the length of this plist node.
        /// </summary>
        /// <returns>The length of this plist node.</returns>
        internal override int BinaryLength => Count;
        /// <summary>
        /// Gets a value indicating whether this instance is written only once in binary mode.
        /// </summary>
        /// <value>
        /// true this instance is written only once in binary mode; otherwise, false
        /// </value>
        internal override bool IsBinaryUnique => false;
        internal override PlistType NodeType => PlistType.Dict;
        /// <summary>
        /// Gets the number of nodes in the dictionary.
        /// </summary>
        /// <value>The count</value>
        public int Count => _dictionary.Count;
        /// <summary>
        /// Gets a value indicating whether this instance is read only.
        /// </summary>
        /// <value>true if this instance is read only, otherwise false.</value>
        public bool IsReadOnly => false;
        /// <summary>
        /// Gets the keys.
        /// </summary>
        /// <value>The keys in the dictionary</value>
        public ICollection<string> Keys => _dictionary.Keys;
        /// <summary>
        /// Gets or sets the <see cref="PropertyNode"/> at the specified index.
        /// </summary>
        /// <param name="key">The dictionary key to retrieve its value</param>
        /// <returns>The value for the specified key.</returns>
        public PropertyNode this[string key] {
            get => _dictionary[key];
            set => _dictionary[key] = value;
        }
        /// <summary>
        /// Gets the values.
        /// </summary>
        /// <value>The values.</value>
        public ICollection<PropertyNode> Values => _dictionary.Values;

        /// <summary>
        /// Gets the enumerator.
        /// </summary>
        /// <returns>The enumerator</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return _dictionary.GetEnumerator();
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

            // make sure we are position at an element, skipping white space and such
            _ = reader.MoveToContent();

            while (reader.NodeType != XmlNodeType.EndElement) {
                reader.ReadStartElement("key");
                string key = reader.ReadContentAsString();
                reader.ReadEndElement();

                reader.MoveToContent();
                PropertyNode node = NodeFactory.Create(reader.LocalName);
                node.ReadXml(reader);
                Add(key, node);

                reader.MoveToContent();
            }

            reader.ReadEndElement();
        }

        /// <summary>
        /// Generates an object from its XML representation.
        /// </summary>
        /// <param name="reader">The <see cref="XmlReader"/> stream from which the object is deserialized.</param>
        internal override async Task ReadXmlAsync(XmlReader reader)
        {
            bool wasEmpty = reader.IsEmptyElement;
            await reader.ReadAsync();
            if (wasEmpty) {
                return;
            }

            // make sure we are position at an element, skipping white space and such
            _ = await reader.MoveToContentAsync();

            while (reader.NodeType != XmlNodeType.EndElement) {
                reader.ReadStartElement("key");
                string key = await reader.ReadContentAsStringAsync();
                reader.ReadEndElement();

                await reader.MoveToContentAsync();
                PropertyNode node = NodeFactory.Create(reader.LocalName);
                await node.ReadXmlAsync(reader);
                Add(key, node);

                await reader.MoveToContentAsync();
            }

            reader.ReadEndElement();
        }

        /// <summary>
        /// Writes this element binary to the writer.
        /// </summary>
        internal override void WriteBinary(Stream stream)
        {
            throw new NotImplementedException("This type of node does not do it's own writing, refer to the binary writer.");
        }

        /// <summary>
        /// Converts an object into its XML representation.
        /// </summary>
        /// <param name="writer">The <see cref="T:System.Xml.XmlWriter"/> stream to which the object is serialized.</param>
        internal override void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement(XmlTag);
            foreach (string key in Keys) {
                writer.WriteStartElement("key");
                writer.WriteValue(key);
                writer.WriteEndElement();
                this[key].WriteXml(writer);
            }
            writer.WriteEndElement();
        }

        /// <summary>
        /// Add the specified key/value pair.
        /// </summary>
        /// <param name="item">Item.</param>
        public void Add(KeyValuePair<string, PropertyNode> item)
        {
            _dictionary.Add(item);
        }

        /// <summary>
        /// Add the specified value with specified key.
        /// </summary>
        /// <param name="key">Key</param>
        /// <param name="value">Value</param>
        public void Add(string key, PropertyNode value)
        {
            _dictionary.Add(key, value);
        }

        /// <summary>
        /// Clear this instance.
        /// </summary>
        public void Clear()
        {
            _dictionary.Clear();
        }

        /// <summary>
        /// Contains the specified key/value pair.
        /// </summary>
        /// <param name="item">Item.</param>
        public bool Contains(KeyValuePair<string, PropertyNode> item)
        {
            return _dictionary.Contains(item);
        }

        /// <summary>
        /// Determines whether the current instance contains an entry with the specified key.
        /// </summary>
        /// <returns>true if the key exists, otherwise false</returns>
        /// <param name="key">Key</param>
        public bool ContainsKey(string key)
        {
            return _dictionary.ContainsKey(key);
        }

        /// <summary>
        /// Copies values.
        /// </summary>
        /// <param name="array">Array</param>
        /// <param name="arrayIndex">Array index</param>
        public void CopyTo(KeyValuePair<string, PropertyNode>[] array, int arrayIndex)
        {
            _dictionary.CopyTo(array, arrayIndex);
        }

        /// <summary>
        /// Gets the enumerator.
        /// </summary>
        /// <returns>The enumerator</returns>
        public IEnumerator<KeyValuePair<string, PropertyNode>> GetEnumerator()
        {
            return _dictionary.GetEnumerator();
        }

        /// <summary>
        /// Remove value at the specified key.
        /// </summary>
        /// <param name="key">Key.</param>
        public bool Remove(string key)
        {
            return _dictionary.Remove(key);
        }

        /// <summary>
        /// Remove the specified key/value pair.
        /// </summary>
        /// <param name="item">Item.</param>
        public bool Remove(KeyValuePair<string, PropertyNode> item)
        {
            return _dictionary.Remove(item);
        }

        /// <summary>
        /// Attempts to retrieve value at the specified key.
        /// </summary>
        /// <returns>true if the key exists, otherwise false</returns>
        /// <param name="key">Key</param>
        /// <param name="value">Value</param>
        public bool TryGetValue(string key, [MaybeNullWhen(false)] out PropertyNode value)
        {
            return _dictionary.TryGetValue(key, out value);
        }
    }
}

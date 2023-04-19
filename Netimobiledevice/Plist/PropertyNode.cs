using Netimobiledevice.Exceptions;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Xml;

namespace Netimobiledevice.Plist
{
    /// <summary>
    /// Base class for every type of node a plist can contain.
    /// </summary>
    public abstract class PropertyNode
    {
        /// <summary>
        /// Gets the binary tag.
        /// </summary>
        /// <value>The binary tag.</value>
        internal byte BinaryTag => (byte) NodeType;
        /// <summary>
        /// Gets the length of the binary representation.
        /// </summary>
        /// <value>The length of the binary value</value>
        internal abstract int BinaryLength { get; }
        internal abstract bool IsBinaryUnique { get; }
        internal abstract PlistType NodeType { get; }
        /// <summary>
        /// Gets the xml tag.
        /// </summary>
        /// <value>The xml tag.</value>
        internal string XmlTag => NodeType.ToEnumMemberAttrValue();

        internal ArrayNode AsArrayNode()
        {
            if (NodeType != PlistType.Array) {
                throw new PlistException($"Invalid type expected {PlistType.Array} found {NodeType}");
            }
            return (ArrayNode) this;
        }

        internal DictionaryNode AsDictionaryNode()
        {
            if (NodeType != PlistType.Dict) {
                throw new PlistException($"Invalid type expected {PlistType.Dict} found {NodeType}");
            }
            return (DictionaryNode) this;
        }

        internal IntegerNode AsIntegerNode()
        {
            if (NodeType != PlistType.Integer) {
                throw new PlistException($"Invalid type expected {PlistType.Integer} found {NodeType}");
            }
            return (IntegerNode) this;
        }

        internal StringNode AsStringNode()
        {
            if (NodeType != PlistType.String && NodeType != PlistType.UString) {
                throw new PlistException($"Invalid type expected {PlistType.String} or {PlistType.UString} found {NodeType}");
            }
            return (StringNode) this;
        }

        internal abstract void ReadBinary(Stream stream, int nodeLength);

        internal abstract void ReadXml(XmlReader reader);

        internal abstract Task ReadXmlAsync(XmlReader reader);

        internal abstract void WriteBinary(Stream stream);

        internal abstract void WriteXml(XmlWriter writer);
    }

    public abstract class PropertyNode<T> : PropertyNode, IEquatable<PropertyNode>
    {
        internal override bool IsBinaryUnique => true;
        /// <summary>
        /// Gets the value.
        /// </summary>
        /// <value>The value</value>
        public virtual T Value { get; set; }

        internal abstract void Parse(string data);

        /// <summary>
        /// Generates an object from its XML representation.
        /// </summary>
        /// <param name="reader">The <see cref="XmlReader"/> stream from which the object is deserialized.</param>
        internal override void ReadXml(XmlReader reader)
        {
            reader.ReadStartElement();
            Parse(reader.ReadContentAsString());
            reader.ReadEndElement();
        }

        /// <summary>
        /// Generates an object from its XML representation.
        /// </summary>
        /// <param name="reader">The <see cref="XmlReader"/> stream from which the object is deserialized.</param>
        internal override async Task ReadXmlAsync(XmlReader reader)
        {
            reader.ReadStartElement();
            Parse(await reader.ReadContentAsStringAsync());
            reader.ReadEndElement();
        }

        internal abstract string ToXmlString();

        /// <summary>
        /// Converts an object into its XML representation.
        /// </summary>
        /// <param name="writer">The <see cref="XmlWriter"/> stream to which the object is serialized.</param>
        internal override void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement(XmlTag);
            writer.WriteValue(ToXmlString());
            writer.WriteEndElement();
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns>
        /// true if the current object is equal to the <paramref name="other"/> parameter, otherwise false.
        /// </returns>
        public bool Equals(PropertyNode other)
        {
            return (other is PropertyNode<T>) && (Value.Equals(((PropertyNode<T>) other).Value));
        }

        /// <summary>
        /// Determines whether the specified <see cref="System.Object"/> is equal to the current PropertyNode.
        /// </summary>
        /// <param name="obj">The <see cref="System.Object"/> to compare with the current PropertyNode.</param>
        /// <returns>true if the specified <see cref="System.Object"/> is equal to the current
        /// PropertyNode, otherwise false</returns>
        public override bool Equals(object obj)
        {
            var node = obj as PropertyNode;
            return node != null && Equals(node);
        }

        /// <summary>
        /// Serves as a hash function for a PropertyNode object.
        /// </summary>
        /// <returns>A hash code for this instance that is suitable for use in hashing algorithms and data structures such as a hash table.</returns>
        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        /// <summary>
        /// Returns a <see cref="System.String"/> that represents the current PropertyNode
        /// </summary>
        /// <returns>A <see cref="System.String"/> that represents the current PropertyNode</returns>
        public override string ToString()
        {
            return $"<{XmlTag}>: {Value}";
        }
    }
}

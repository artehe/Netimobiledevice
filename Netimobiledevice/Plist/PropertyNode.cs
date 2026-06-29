using System;
using System.IO;
using System.Threading.Tasks;
using System.Xml;

namespace Netimobiledevice.Plist;

/// <summary>
/// Base class for every type of node a plist can contain.
/// </summary>
public abstract class PropertyNode {
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
    internal string XmlTag => NodeType.GetXmlTag();

    private T As<T>(PlistType expected) where T : PropertyNode {
        if (NodeType != expected) {
            throw new PlistException($"Invalid type. Expected {expected} but found {NodeType}");
        }
        return (T) this;
    }

    private T As<T>(params PlistType[] expected) where T : PropertyNode {
        if (!expected.Contains(NodeType)) {
            throw new PlistException($"Invalid type. Expected {string.Join(" or ", expected)} but found {NodeType}");
        }
        return (T) this;
    }

    internal abstract void ReadBinary(Stream stream, int nodeLength);

    internal abstract Task ReadBinaryAsync(Stream stream, int nodeLength);

    internal abstract void ReadXml(XmlReader reader);

    internal abstract Task ReadXmlAsync(XmlReader reader);

    internal abstract void WriteBinary(Stream stream);

    internal abstract Task WriteBinaryAsync(Stream stream);

    internal abstract void WriteXml(XmlWriter writer);

    internal abstract Task WriteXmlAsync(XmlWriter writer);

    public ArrayNode AsArrayNode() => As<ArrayNode>(PlistType.Array);

    public BooleanNode AsBooleanNode() => As<BooleanNode>(PlistType.Boolean);

    public DataNode AsDataNode() => As<DataNode>(PlistType.Data);

    public DateNode AsDateNode() => As<DateNode>(PlistType.Date);

    public DictionaryNode AsDictionaryNode() => As<DictionaryNode>(PlistType.Dict);

    public FillNode AsFillNode() => As<FillNode>(PlistType.Fill);

    public IntegerNode AsIntegerNode() => As<IntegerNode>(PlistType.Integer);

    public NullNode AsNullNode() => As<NullNode>(PlistType.Null);

    public RealNode AsRealNode() => As<RealNode>(PlistType.Real);

    public StringNode AsStringNode() => As<StringNode>(PlistType.String, PlistType.UString);

    public UidNode AsUidNode() => As<UidNode>(PlistType.Uid);
}

public abstract class PropertyNode<T>(T value) : PropertyNode, IEquatable<PropertyNode> {
    protected T _value = value;

    internal override bool IsBinaryUnique => true;
    /// <summary>
    /// Gets the value.
    /// </summary>
    /// <value>The value</value>
    public virtual T Value {
        get => _value;
        protected set {
            _value = value;
        }
    }

    internal abstract void Parse(string data);

    /// <summary>
    /// Generates an object from its XML representation.
    /// </summary>
    /// <param name="reader">The <see cref="XmlReader"/> stream from which the object is deserialized.</param>
    internal override void ReadXml(XmlReader reader) {
        reader.ReadStartElement();
        Parse(reader.ReadContentAsString());
        reader.ReadEndElement();
    }

    /// <summary>
    /// Generates an object from its XML representation.
    /// </summary>
    /// <param name="reader">The <see cref="XmlReader"/> stream from which the object is deserialized.</param>
    internal override async Task ReadXmlAsync(XmlReader reader) {
        reader.ReadStartElement();
        Parse(await reader.ReadContentAsStringAsync());
        reader.ReadEndElement();
    }

    internal abstract string ToXmlString();

    /// <summary>
    /// Converts an object into its XML representation.
    /// </summary>
    /// <param name="writer">The <see cref="XmlWriter"/> stream to which the object is serialized.</param>
    internal override void WriteXml(XmlWriter writer) {
        writer.WriteStartElement(XmlTag);
        writer.WriteString(ToXmlString());
        writer.WriteEndElement();
    }

    /// <summary>
    /// Converts an object into its XML representation.
    /// </summary>
    /// <param name="writer">The <see cref="XmlWriter"/> stream to which the object is serialized.</param>
    internal override async Task WriteXmlAsync(XmlWriter writer) {
        await writer.WriteStartElementAsync(null, XmlTag, null).ConfigureAwait(false);
        await writer.WriteStringAsync(ToXmlString()).ConfigureAwait(false);
        await writer.WriteEndElementAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Indicates whether the current object is equal to another object of the same type.
    /// </summary>
    /// <param name="other">An object to compare with this object.</param>
    /// <returns>
    /// true if the current object is equal to the <paramref name="other"/> parameter, otherwise false.
    /// </returns>
    public bool Equals(PropertyNode? other) {
        if (other is not PropertyNode<T> node) {
            return false;
        }
        if (NodeType != other.NodeType) {
            return false;
        }
        if (Value is null) {
            return node.Value is null;
        }
        return Value.Equals(node.Value);
    }

    /// <summary>
    /// Determines whether the specified <see cref="object"/> is equal to the current PropertyNode.
    /// </summary>
    /// <param name="obj">The <see cref="object"/> to compare with the current PropertyNode.</param>
    /// <returns>true if the specified <see cref="object"/> is equal to the current
    /// PropertyNode, otherwise false</returns>
    public override bool Equals(object? obj) {
        return obj is PropertyNode node && Equals(node);
    }

    /// <summary>
    /// Serves as a hash function for a PropertyNode object.
    /// </summary>
    /// <returns>A hash code for this instance that is suitable for use in hashing algorithms and data structures such as a hash table.</returns>
    public override int GetHashCode() => HashCode.Combine(NodeType, Value);

    /// <summary>
    /// Returns a <see cref="string"/> that represents the current PropertyNode
    /// </summary>
    /// <returns>A <see cref="string"/> that represents the current PropertyNode</returns>
    public override string ToString() {
        return $"<{XmlTag}>: {Value}";
    }
}

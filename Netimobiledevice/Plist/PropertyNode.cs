using System;
using System.IO;
using System.Threading.Tasks;
using System.Xml;

namespace Netimobiledevice.Plist;

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

    internal abstract void ReadBinary(Stream stream, int nodeLength);

    internal abstract Task ReadBinaryAsync(Stream stream, int nodeLength);

    internal abstract void ReadXml(XmlReader reader);

    internal abstract Task ReadXmlAsync(XmlReader reader);

    internal abstract void WriteBinary(Stream stream);

    internal abstract Task WriteBinaryAsync(Stream stream);

    internal abstract void WriteXml(XmlWriter writer);

    internal abstract Task WriteXmlAsync(XmlWriter writer);

    public ArrayNode AsArrayNode()
    {
        if (NodeType != PlistType.Array) {
            throw new PlistException($"Invalid type expected {PlistType.Array} found {NodeType}");
        }
        return (ArrayNode) this;
    }

    public BooleanNode AsBooleanNode()
    {
        if (NodeType != PlistType.Boolean) {
            throw new PlistException($"Invalid type expected {PlistType.Boolean} found {NodeType}");
        }
        return (BooleanNode) this;
    }

    public DataNode AsDataNode()
    {
        if (NodeType != PlistType.Data) {
            throw new PlistException($"Invalid type expected {PlistType.Data} found {NodeType}");
        }
        return (DataNode) this;
    }

    public DateNode AsDateNode()
    {
        if (NodeType != PlistType.Date) {
            throw new PlistException($"Invalid type expected {PlistType.Date} found {NodeType}");
        }
        return (DateNode) this;
    }

    public DictionaryNode AsDictionaryNode()
    {
        if (NodeType != PlistType.Dict) {
            throw new PlistException($"Invalid type expected {PlistType.Dict} found {NodeType}");
        }
        return (DictionaryNode) this;
    }

    public FillNode AsFillNode()
    {
        if (NodeType != PlistType.Fill) {
            throw new PlistException($"Invalid type expected {PlistType.Fill} found {NodeType}");
        }
        return (FillNode) this;
    }

    public IntegerNode AsIntegerNode()
    {
        if (NodeType != PlistType.Integer) {
            throw new PlistException($"Invalid type expected {PlistType.Integer} found {NodeType}");
        }
        return (IntegerNode) this;
    }

    public NullNode AsNullNode()
    {
        if (NodeType != PlistType.Null) {
            throw new PlistException($"Invalid type expected {PlistType.Null} found {NodeType}");
        }
        return (NullNode) this;
    }

    public RealNode AsRealNode()
    {
        if (NodeType != PlistType.Real) {
            throw new PlistException($"Invalid type expected {PlistType.Real} found {NodeType}");
        }
        return (RealNode) this;
    }

    public StringNode AsStringNode()
    {
        if (NodeType != PlistType.String && NodeType != PlistType.UString) {
            throw new PlistException($"Invalid type expected {PlistType.String} or {PlistType.UString} found {NodeType}");
        }
        return (StringNode) this;
    }

    public UidNode AsUidNode()
    {
        if (NodeType != PlistType.Uid) {
            throw new PlistException($"Invalid type expected {PlistType.Uid} found {NodeType}");
        }
        return (UidNode) this;
    }
}

public abstract class PropertyNode<T>(T value) : PropertyNode, IEquatable<PropertyNode>
{
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
    /// Converts an object into its XML representation.
    /// </summary>
    /// <param name="writer">The <see cref="XmlWriter"/> stream to which the object is serialized.</param>
    internal override async Task WriteXmlAsync(XmlWriter writer)
    {
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
    public bool Equals(PropertyNode? other)
    {
        if (other is PropertyNode<T> node && Value != null) {
            return Value.Equals(node.Value);
        }
        return false;
    }

    /// <summary>
    /// Determines whether the specified <see cref="object"/> is equal to the current PropertyNode.
    /// </summary>
    /// <param name="obj">The <see cref="object"/> to compare with the current PropertyNode.</param>
    /// <returns>true if the specified <see cref="object"/> is equal to the current
    /// PropertyNode, otherwise false</returns>
    public override bool Equals(object? obj)
    {
        return obj is PropertyNode node && Equals(node);
    }

    /// <summary>
    /// Serves as a hash function for a PropertyNode object.
    /// </summary>
    /// <returns>A hash code for this instance that is suitable for use in hashing algorithms and data structures such as a hash table.</returns>
    public override int GetHashCode()
    {
        return Value?.GetHashCode() ?? -1;
    }

    /// <summary>
    /// Returns a <see cref="string"/> that represents the current PropertyNode
    /// </summary>
    /// <returns>A <see cref="string"/> that represents the current PropertyNode</returns>
    public override string ToString()
    {
        return $"<{XmlTag}>: {Value}";
    }
}

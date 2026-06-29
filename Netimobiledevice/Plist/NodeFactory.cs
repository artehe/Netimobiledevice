using System;
using System.Collections.Generic;

namespace Netimobiledevice.Plist;

/// <summary>
/// Singleton class which generates concrete <see cref="PropertyNode"/> from the Tag or TypeCode
/// </summary>
internal static class NodeFactory {
    private static readonly Dictionary<byte, PlistType> _binaryTags = [];
    private static readonly Dictionary<string, PlistType> _xmlTags = [];

    /// <summary>
    /// Initializes the <see cref="NodeFactory"/> class.
    /// </summary>
    static NodeFactory() {
        Register(new DictionaryNode());
        Register(new IntegerNode());
        Register(new RealNode());
        Register(new StringNode());
        Register(new ArrayNode());
        Register(new DataNode());
        Register(new DateNode());
        Register(new UidNode());

        Register("string", 5, new StringNode());
        Register("ustring", 6, new StringNode());

        Register("true", 0, new BooleanNode());
        Register("false", 0, new BooleanNode());
    }

    private static PropertyNode GetPropertyNodeFromType(PlistType type) => type switch {
        PlistType.Array => new ArrayNode(),
        PlistType.Boolean or PlistType.Bool => new BooleanNode(),
        PlistType.Data => new DataNode(),
        PlistType.Date => new DateNode(),
        PlistType.Dict => new DictionaryNode(),
        PlistType.Fill => new FillNode(),
        PlistType.Integer => new IntegerNode(),
        PlistType.Null => new NullNode(),
        PlistType.Real => new RealNode(),
        PlistType.String or PlistType.UString => new StringNode(),
        PlistType.Uid => new UidNode(),
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    /// <summary>
    /// Registers the specified element.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="node">The node.</param>
    private static void Register(PropertyNode node) {
        _binaryTags.TryAdd(node.BinaryTag, node.NodeType);
        _xmlTags.TryAdd(node.XmlTag, node.NodeType);
    }

    /// <summary>
    /// Registers the specified element.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="xmlTag">The tag.</param>
    /// <param name="binaryTag">The type code.</param>
    /// <param name="node">The element.</param>
    private static void Register(string xmlTag, byte binaryTag, PropertyNode node) {
        if (!_binaryTags.ContainsKey(binaryTag)) {
            _binaryTags.Add(binaryTag, node.NodeType);
        }
        if (!_xmlTags.ContainsKey(xmlTag)) {
            _xmlTags.Add(xmlTag, node.NodeType);
        }
    }

    /// <summary>        
    /// Creates a concrete <see cref="PropertyNode"/> object specified by it's tag.
    /// </summary>
    /// <param name="tag">The tag of the element.</param>
    /// <returns>The created <see cref="PropertyNode"/> object</returns>
    public static PropertyNode Create(string tag) {
        if (_xmlTags.TryGetValue(tag, out PlistType type)) {
            return GetPropertyNodeFromType(type);
        }
        throw new PlistFormatException($"Unknown node - XML tag \"{tag}\"");
    }

    /// <summary>
    /// Creates a concrete <see cref="PropertyNode"/> object secified specified by it's typecode.
    /// </summary>
    /// <param name="binaryTag">The typecode of the element.</param>
    /// <param name="length">The length of the element</param>
    /// <returns>The created <see cref="PropertyNode"/> object</returns>
    public static PropertyNode Create(byte binaryTag, int length) {
        byte shortBinaryTag = (byte) (binaryTag & 0xF0);

        if (shortBinaryTag == 0) {
            if (length == 0x00) {
                return new NullNode();
            }
            if (length == 0x0F) {
                return new FillNode();
            }
        }

        if (shortBinaryTag == 0x60) {
            return new StringNode {
                IsUtf16 = true
            };
        }
        if (_binaryTags.TryGetValue(shortBinaryTag, out PlistType type)) {
            return GetPropertyNodeFromType(type);
        }

        throw new PlistFormatException($"Unknown node - binary tag {binaryTag}");
    }

    /// <summary>
    /// Creates a <see cref="PropertyNode"/> object used for dictionary keys.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The <see cref="PropertyNode"/> object used for dictionary keys.</returns>
    public static PropertyNode CreateKeyElement(string key) {
        return new StringNode(key);
    }

    /// <summary>
    /// Creates a <see cref="PropertyNode"/> object used for extended length information.
    /// </summary>
    /// <param name="length">The extended length information.</param>
    /// <returns>The <see cref="PropertyNode"/> object used for extended length information.</returns>
    public static PropertyNode CreateLengthElement(int length) {
        return new IntegerNode(length);
    }
}

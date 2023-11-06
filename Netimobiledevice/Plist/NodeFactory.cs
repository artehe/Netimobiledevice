using Netimobiledevice.Exceptions;
using System;
using System.Collections.Generic;

namespace Netimobiledevice.Plist
{
    /// <summary>
	/// Singleton class which generates concrete <see cref="PropertyNode"/> from the Tag or TypeCode
	/// </summary>
    internal static class NodeFactory
    {
        private static readonly Dictionary<byte, PropertyNode> _binaryTags = new Dictionary<byte, PropertyNode>();
        private static readonly Dictionary<string, PropertyNode> _xmlTags = new Dictionary<string, PropertyNode>();

        /// <summary>
		/// Initializes the <see cref="NodeFactory"/> class.
		/// </summary>
		static NodeFactory()
        {
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

        /// <summary>
		/// Registers the specified element.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="node">The node.</param>
		private static void Register<T>(T node) where T : PropertyNode, new()
        {
            if (!_binaryTags.ContainsKey(node.BinaryTag)) {
                _binaryTags.Add(node.BinaryTag, node);
            }
            if (!_xmlTags.ContainsKey(node.XmlTag)) {
                _xmlTags.Add(node.XmlTag, node);
            }
        }

        /// <summary>
		/// Registers the specified element.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="xmlTag">The tag.</param>
		/// <param name="binaryTag">The type code.</param>
		/// <param name="node">The element.</param>
		private static void Register<T>(string xmlTag, byte binaryTag, T node) where T : PropertyNode, new()
        {
            if (!_binaryTags.ContainsKey(binaryTag)) {
                _binaryTags.Add(binaryTag, node);
            }
            if (!_xmlTags.ContainsKey(xmlTag)) {
                _xmlTags.Add(xmlTag, node);
            }
        }

        /// <summary>        
		/// Creates a concrete <see cref="PropertyNode"/> object secified specified by it's tag.
		/// </summary>
		/// <param name="tag">The tag of the element.</param>
		/// <returns>The created <see cref="PropertyNode"/> object</returns>
		public static PropertyNode Create(string tag)
        {
            if (_xmlTags.ContainsKey(tag)) {
                return (PropertyNode?) Activator.CreateInstance(_xmlTags[tag].GetType()) ?? new NullNode();
            }
            throw new PlistFormatException($"Unknown node - XML tag \"{tag}\"");
        }

        /// <summary>
		/// Creates a concrete <see cref="PropertyNode"/> object secified specified by it's typecode.
		/// </summary>
		/// <param name="binaryTag">The typecode of the element.</param>
		/// <param name="length">The length of the element</param>
		/// <returns>The created <see cref="PropertyNode"/> object</returns>
		public static PropertyNode Create(byte binaryTag, int length)
        {
            if (binaryTag == 0 && length == 0x00) {
                return new NullNode();
            }
            if (binaryTag == 0 && length == 0x0F) {
                return new FillNode();
            }

            if (binaryTag == 6) {
                return new StringNode { IsUtf16 = true };
            }
            if (_binaryTags.ContainsKey(binaryTag)) {
                return (PropertyNode?) Activator.CreateInstance(_binaryTags[binaryTag].GetType()) ?? new NullNode();
            }

            throw new PlistFormatException($"Unknown node - binary tag {binaryTag}");
        }

        /// <summary>
		/// Creates a <see cref="PropertyNode"/> object used for dictionary keys.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <returns>The <see cref="PropertyNode"/> object used for dictionary keys.</returns>
		public static PropertyNode CreateKeyElement(string key)
        {
            return new StringNode(key);
        }

        /// <summary>
		/// Creates a <see cref="PropertyNode"/> object used for exteded length information.
		/// </summary>
		/// <param name="length">The exteded length information.</param>
		/// <returns>The <see cref="PropertyNode"/> object used for exteded length information.</returns>
		public static PropertyNode CreateLengthElement(int length)
        {
            return new IntegerNode(length);
        }
    }
}

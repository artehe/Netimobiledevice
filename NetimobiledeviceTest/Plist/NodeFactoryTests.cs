using Netimobiledevice.Plist;

namespace NetimobiledeviceTest.Plist;

[TestClass]
public class NodeFactoryTests {
    [TestMethod]
    [DataRow("string", typeof(StringNode))]
    [DataRow("true", typeof(BooleanNode))]
    [DataRow("false", typeof(BooleanNode))]
    public void Create_FromXmlTag_ReturnsCorrectType(string tag, Type expectedType) {
        PropertyNode node = NodeFactory.Create(tag);
        Assert.IsNotNull(node);
        Assert.AreEqual(expectedType, node.GetType());
    }

    [TestMethod]
    public void Create_FromXmlTag_UnknownTag_Throws() {
        Assert.ThrowsExactly<PlistFormatException>(() => NodeFactory.Create("unknown-tag"));
    }

    [TestMethod]
    public void Create_FromBinaryTag_NullNode() {
        PropertyNode node = NodeFactory.Create(0x00, 0x00);
        Assert.IsInstanceOfType<NullNode>(node);
    }

    [TestMethod]
    public void Create_FromBinaryTag_FillNode() {
        PropertyNode node = NodeFactory.Create(0x00, 0x0F);
        Assert.IsInstanceOfType<FillNode>(node);
    }

    [TestMethod]
    public void Create_FromBinaryTag_Utf16StringNode() {
        PropertyNode node = NodeFactory.Create(0x60, 0x01);
        Assert.IsInstanceOfType<StringNode>(node);
        StringNode stringNode = node.AsStringNode();
        Assert.IsTrue(stringNode.IsUtf16);
    }

    [TestMethod]
    public void Create_FromBinaryTag_KnownType_ReturnsCorrectNode() {
        // Adjust tag if your actual BinaryTag differs
        PropertyNode node = NodeFactory.Create(0x10, 0x01);
        Assert.IsInstanceOfType<IntegerNode>(node);
    }

    [TestMethod]
    public void Create_FromBinaryTag_Unknown_Throws() {
        Assert.ThrowsExactly<PlistFormatException>(() => NodeFactory.Create(0xFF, 0x01));
    }

    [TestMethod]
    public void CreateKeyElement_ReturnsStringNode_WithValue() {
        string key = "myKey";
        PropertyNode node = NodeFactory.CreateKeyElement(key);
        Assert.IsInstanceOfType<StringNode>(node);
        StringNode stringNode = node.AsStringNode();
        Assert.AreEqual(key, stringNode.Value);
    }

    [TestMethod]
    public void CreateLengthElement_ReturnsIntegerNode_WithValue() {
        int length = 42;
        PropertyNode node = NodeFactory.CreateLengthElement(length);
        Assert.IsInstanceOfType<IntegerNode>(node);
        IntegerNode intNode = node.AsIntegerNode();
        Assert.AreEqual(length, (int) intNode.Value);
    }

    [TestMethod]
    public void Create_FromXmlTag_StringVariants_ReturnSameType() {
        PropertyNode node1 = NodeFactory.Create("string");
        PropertyNode node2 = NodeFactory.Create("ustring");

        Assert.AreEqual(node1.GetType(), node2.GetType());
        Assert.IsInstanceOfType<StringNode>(node1);
    }

    [TestMethod]
    public void Create_FromBinaryTag_MasksLowerBitsCorrectly() {
        PropertyNode node1 = NodeFactory.Create(0x10, 0x01);
        PropertyNode node2 = NodeFactory.Create(0x1F, 0x01);
        Assert.AreEqual(node1.GetType(), node2.GetType());
    }
}

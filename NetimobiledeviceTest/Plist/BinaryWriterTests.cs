using Netimobiledevice.Plist;

namespace NetimobiledeviceTest.Plist;

[TestClass]
public class BinaryWriterTests
{
    [TestMethod]
    public void WhenBooleanValueIsSavedCorrectly()
    {
        // Create basic Plist containing both types of boolean value
        DictionaryNode node = new DictionaryNode {
            { "Test", new BooleanNode(true) },
            { "Test2", new BooleanNode(false) }
        };

        byte[] binaryPlist = PropertyList.SaveAsByteArray(node, PlistFormat.Binary);

        DictionaryNode reReadNode = PropertyList.LoadFromByteArray(binaryPlist).AsDictionaryNode();
        Assert.IsTrue(reReadNode["Test"].AsBooleanNode().Value);
        Assert.IsFalse(reReadNode["Test2"].AsBooleanNode().Value);
    }

    [TestMethod]
    public void WhenStringContainsUnicodeThenStringIsWrappedAsUstring()
    {
        // Create basic Plist
        string utf16value = "😂test";
        StringNode stringNode = new StringNode(utf16value);
        DictionaryNode node = new DictionaryNode { { "Test", stringNode } };

        byte[] binaryPlist = PropertyList.SaveAsByteArray(node, PlistFormat.Binary);

        DictionaryNode reReadNode = PropertyList.LoadFromByteArray(binaryPlist).AsDictionaryNode();
        Assert.IsTrue(reReadNode["Test"].AsStringNode().IsUtf16);
        Assert.AreEqual(utf16value, reReadNode["Test"].AsStringNode().Value);
    }

    [TestMethod]
    public void ConvertsPlistArrayCorrectly()
    {
        ArrayNode arrayNode = [
            new StringNode("DLMessageVersionExchange"),
            new StringNode("DLVersionsOk"),
            new IntegerNode(400)
        ];

        byte[] plistBytes = PropertyList.SaveAsByteArray(arrayNode, PlistFormat.Binary);

        ArrayNode reReadNode = PropertyList.LoadFromByteArray(plistBytes).AsArrayNode();

        Assert.AreEqual(arrayNode[0].AsStringNode().Value, reReadNode[0].AsStringNode().Value);
        Assert.AreEqual(arrayNode[1].AsStringNode().Value, reReadNode[1].AsStringNode().Value);
        Assert.AreEqual(arrayNode[2].AsIntegerNode().Value, reReadNode[2].AsIntegerNode().Value);
    }

    [TestMethod]
    public void ConvertsPlistDictionaryCorrectly()
    {
        string request = "StartActivity";
        int messageFilter = 65535;
        int pid = -1;

        DictionaryNode dictNode = new DictionaryNode() {
            { "Request", new StringNode(request) },
            { "MessageFilter", new IntegerNode(messageFilter) },
            { "Pid", new IntegerNode(pid) },
        };

        byte[] plistBytes = PropertyList.SaveAsByteArray(dictNode, PlistFormat.Binary);

        DictionaryNode reReadNode = PropertyList.LoadFromByteArray(plistBytes).AsDictionaryNode();

        Assert.AreEqual(request, reReadNode["Request"].AsStringNode().Value);
        Assert.AreEqual(messageFilter, (int) reReadNode["MessageFilter"].AsIntegerNode().Value);
        Assert.AreEqual(pid, (int) reReadNode["Pid"].AsIntegerNode().Value);
    }
}

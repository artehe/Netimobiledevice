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
        Assert.AreEqual(true, reReadNode["Test"].AsBooleanNode().Value);
        Assert.AreEqual(false, reReadNode["Test2"].AsBooleanNode().Value);
    }

    [TestMethod]
    public void WhenStringContainsUnicodeThenStringIsWrappedAsUstring()
    {
        // Create basic Plist
        string utf16value = "😂test";
        DictionaryNode node = new DictionaryNode { { "Test", new StringNode(utf16value) } };

        byte[] binaryPlist = PropertyList.SaveAsByteArray(node, PlistFormat.Binary);

        DictionaryNode reReadNode = PropertyList.LoadFromByteArray(binaryPlist).AsDictionaryNode();
        Assert.AreEqual(true, reReadNode["Test"].AsStringNode().IsUtf16);
        Assert.AreEqual(utf16value, reReadNode["Test"].AsStringNode().Value);
    }
}

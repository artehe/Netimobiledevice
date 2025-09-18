using Netimobiledevice.Plist;

namespace NetimobiledeviceTest.Plist;

[TestClass]
public class DictionaryNodeTests
{

    [TestMethod]
    public void DictionaryNodeReturnsCorrectCount()
    {
        DictionaryNode dict = [];
        Assert.AreEqual(0, dict.Count);

        dict.Add("Zero", new IntegerNode(0));
        dict.Add("One", new IntegerNode(1));
        dict.Add("Two", new IntegerNode(2));

        Assert.AreEqual(3, dict.Count);
    }

    [TestMethod]
    public void DictionaryNodeReturnsCorrectKeys()
    {
        DictionaryNode dict = new DictionaryNode {
            { "Zero", new IntegerNode(0) },
            { "One", new IntegerNode(1) },
            { "Two", new IntegerNode(2) }
        };

        List<string> keys = [.. dict.Keys];
        Assert.AreEqual("Zero", keys[0]);
        Assert.AreEqual("One", keys[1]);
        Assert.AreEqual("Two", keys[2]);
    }

    [TestMethod]
    public void DictionaryNodeReturnsCorrectValueFromKey()
    {
        DictionaryNode dict = new DictionaryNode {
            { "Test", new IntegerNode(12) }
        };
        Assert.AreEqual<ulong>(12, ((IntegerNode) dict["Test"]).Value);
    }

    [TestMethod]
    public void DictionaryNodeEnumeratesCorrectly()
    {
        DictionaryNode dict = new DictionaryNode {
            { "Zero", new IntegerNode(0) },
            { "One", new IntegerNode(1) },
            { "Two", new IntegerNode(2) }
        };

        ulong index = 0;
        foreach (KeyValuePair<string, PropertyNode> item in dict) {
            IntegerNode node = (IntegerNode) item.Value;
            Assert.AreEqual(index, node.Value);
            index++;
        }
    }

    [TestMethod]
    public void DictionaryTryGetValueReturnsCorrectly()
    {
        DictionaryNode dict = new DictionaryNode {
            { "Zero", new IntegerNode(0) },
            { "One", new IntegerNode(1) },
            { "Two", new IntegerNode(2) }
        };

        Assert.IsTrue(dict.TryGetValue("One", out PropertyNode? value));
        Assert.IsInstanceOfType(value, typeof(IntegerNode));
        Assert.AreEqual(1, (int) value.AsIntegerNode().Value);

        Assert.IsFalse(dict.TryGetValue("Three", out PropertyNode? value2));
        Assert.IsNull(value2);
    }
}

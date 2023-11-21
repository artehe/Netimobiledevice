using Netimobiledevice.Plist;

namespace NetimobiledeviceTest.Plist;

[TestClass]
public class IntegerNodeTests
{
    [TestMethod]
    public void CreatesLongCorrectly()
    {
        IntegerNode node = new IntegerNode(ulong.MaxValue);
        ulong returnedValue = node.Value;
        Assert.AreEqual(ulong.MaxValue, returnedValue);

        node = new IntegerNode(ulong.MinValue);
        returnedValue = node.Value;
        Assert.AreEqual(ulong.MinValue, returnedValue);
    }

    [TestMethod]
    public void ToStringReturnsNodeType()
    {
        IntegerNode node = new IntegerNode(-4);
        Assert.AreEqual($"<integer>: -4", node.ToString());

        node = new IntegerNode(274);
        Assert.AreEqual($"<integer>: 274", node.ToString());
    }

    [TestMethod]
    public void ParseHandlesNegativeValue()
    {
        IntegerNode node = new IntegerNode();
        node.Parse("-536854523");

        int actualValue = (int) node.Value;
        Assert.AreEqual(-536854523, actualValue);
    }
}

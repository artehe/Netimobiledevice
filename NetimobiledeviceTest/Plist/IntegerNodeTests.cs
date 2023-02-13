using Netimobiledevice.Plist;

namespace NetimobiledeviceTest.Plist;

[TestClass]
public class IntegerNodeTests
{
    [TestMethod]
    public void CreatesLongCorrectly()
    {
        IntegerNode node = new IntegerNode(long.MaxValue);
        long returnedValue = node.Value;
        Assert.AreEqual(long.MaxValue, returnedValue);

        node = new IntegerNode(long.MinValue);
        returnedValue = node.Value;
        Assert.AreEqual(long.MinValue, returnedValue);
    }

    [TestMethod]
    public void ToStringReturnsNodeType()
    {
        IntegerNode node = new IntegerNode(-4);
        Assert.AreEqual($"<integer>: -4", node.ToString());

        node = new IntegerNode(274);
        Assert.AreEqual($"<integer>: 274", node.ToString());
    }
}

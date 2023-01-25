using Netimobiledevice.Plist;

namespace NetimobiledeviceTest.Plist;

[TestClass]
public class RealNodeTests
{
    [TestMethod]
    public void CreatesRealCorrectly()
    {
        RealNode node = new RealNode(123456);
        double value = node.Value;
        Assert.AreEqual(123456, value);
    }

    [TestMethod]
    public void CreatesNegativeRealCorrectly()
    {
        RealNode node = new RealNode(-654321);
        double value = node.Value;
        Assert.AreEqual(-654321, value);
    }

    [TestMethod]
    public void CreatesDecimalNumberCorrectly()
    {
        RealNode node = new RealNode(1234.56);
        double value = node.Value;
        Assert.AreEqual(1234.56, value);
    }

    [TestMethod]
    public void ToStringReturnsNodeType()
    {
        RealNode node = new RealNode(4.5);
        Assert.AreEqual($"<real>: 4.5", node.ToString());
    }

    [TestMethod]
    public void PropertyNodeConvertsToDouble()
    {
        PropertyNode node = new RealNode(1234.56);
        double value = ((RealNode) node).Value;
        Assert.AreEqual(1234.56, value);
    }
}

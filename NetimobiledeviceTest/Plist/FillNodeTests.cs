using Netimobiledevice.Plist;

namespace NetimobiledeviceTest.Plist;

[TestClass]
public class FillNodeTests
{
    [TestMethod]
    public void ToStringReturnsNodeType()
    {
        NullNode node = new NullNode();
        Assert.AreEqual($"<fill>", node.ToString());
    }
}

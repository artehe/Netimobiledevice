using Netimobiledevice.Plist;

namespace NetimobiledeviceTest.Plist;

[TestClass]
public class NullNodeTests
{
    [TestMethod]
    public void ToStringReturnsNodeType()
    {
        NullNode node = new NullNode();
        Assert.AreEqual($"<boolean>", node.ToString());
    }
}

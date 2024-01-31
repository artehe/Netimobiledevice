using Netimobiledevice.Plist;

namespace NetimobiledeviceTest.Plist;

[TestClass]
public class FillNodeTests
{
    [TestMethod]
    public void ToStringReturnsNodeType()
    {
        FillNode node = new FillNode();
        Assert.AreEqual($"<fill>", node.ToString());
    }
}

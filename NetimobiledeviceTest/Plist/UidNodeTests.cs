using Netimobiledevice.Plist;

namespace NetimobiledeviceTest.Plist;

[TestClass]
public class UidNodeTests
{
    [TestMethod]
    public void HandlesUidCorrectly()
    {
        UidNode minNode = new UidNode(ulong.MinValue);
        ulong value = minNode.Value;
        Assert.AreEqual(ulong.MinValue, value);

        UidNode maxNode = new UidNode(ulong.MaxValue);
        value = maxNode.Value;
        Assert.AreEqual(ulong.MaxValue, value);
    }

    [TestMethod]
    public void ToStringReturnsNodeType()
    {
        UidNode node = new UidNode(3);
        Assert.AreEqual($"<uid>: 3", node.ToString());
    }
}

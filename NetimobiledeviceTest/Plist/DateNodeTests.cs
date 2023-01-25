using Netimobiledevice.Plist;

namespace NetimobiledeviceTest.Plist;

[TestClass]
public class DateNodeTests
{
    [TestMethod]
    public void PropertyCreatesNewDate()
    {
        DateTime currentTime = new DateTime(2022, 04, 18, 11, 58, 31, DateTimeKind.Utc);
        DateNode node = new DateNode(currentTime);
        Assert.AreEqual(currentTime, node.Value);
    }

    [TestMethod]
    public void ToStringReturnsNodeType()
    {
        DateTime currentTime = new DateTime(2022, 04, 18, 11, 58, 31, DateTimeKind.Utc);
        DateNode node = new DateNode(currentTime);
        Assert.AreEqual($"<date>: {currentTime}", node.ToString());
    }

    [TestMethod]
    public void PropertyNodeConvertsToDateTime()
    {
        DateTime currentTime = new DateTime(2022, 04, 18, 11, 58, 31, DateTimeKind.Utc);
        PropertyNode node = new DateNode(currentTime);
        DateTime value = ((DateNode) node).Value;
        Assert.AreEqual(currentTime, value);
    }
}

using Netimobiledevice.Plist;
using System.Globalization;

namespace NetimobiledeviceTest.Plist;

[TestClass]
public class DateNodeTests
{
    private static void ExecuteWithCulture(Action methodFunc, CultureInfo culture)
    {
        var thread = new Thread(() => {
            methodFunc();
        }) {
            CurrentCulture = culture
        };
        thread.Start();
        thread.Join();

        return;
    }

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

    public static void DateHandlesArabicCulture()
    {
        DateTime originalDateTime = new DateTime(2015, 9, 30, 0, 0, 0, DateTimeKind.Utc);
        DateNode node = new DateNode(originalDateTime);
        Assert.AreEqual("2015-09-30T00:00:00.000000Z", node.ToXmlString());
        Assert.AreEqual(originalDateTime, node.Value);

        DateTime alternativeDateTime = new DateTime(455874381151831020, DateTimeKind.Utc);
        DateNode alternativeNode = new DateNode(alternativeDateTime);
        Assert.AreEqual("1445-08-11T09:15:15.183102Z", alternativeNode.ToXmlString());
        Assert.AreEqual(alternativeDateTime, alternativeNode.Value);
    }

    [TestMethod]
    public void DateHandlesArabicCultureTest()
    {
        var cultureInfo = new CultureInfo("ar-SA");
        cultureInfo.DateTimeFormat.Calendar = new UmAlQuraCalendar();
        ExecuteWithCulture(() => DateHandlesArabicCulture(), cultureInfo);
    }
}

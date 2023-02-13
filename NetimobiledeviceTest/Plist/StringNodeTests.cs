using Netimobiledevice.Plist;

namespace NetimobiledeviceTest.Plist;

[TestClass]
public class StringNodeTests
{
    [TestMethod]
    public void ToStringReturnsNodeType()
    {
        StringNode value = new StringNode("Random string goes in here");
        Assert.AreEqual($"<string>: Random string goes in here", value.ToString());
    }


    [TestMethod]
    public void StringNodeCreatesNewStrings()
    {
        StringNode node = new StringNode("Basic Test String");
        Assert.AreEqual("Basic Test String", node.Value);
    }

    [TestMethod]
    public void HandlesSpecialCharactersInString()
    {
        string testString = "A - new & Random & String &";
        StringNode node = new StringNode(testString);
        Assert.AreEqual(testString, node.Value);
    }
}

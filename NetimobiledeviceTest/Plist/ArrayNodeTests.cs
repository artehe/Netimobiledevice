using Netimobiledevice.Plist;

namespace NetimobiledeviceTest.Plist;

[TestClass]
public class ArrayNodeTests
{
    [TestMethod]
    public void ArrayNodeHandlesOutOfRangeIndex()
    {
        ArrayNode property = [
            new IntegerNode(0),
            new IntegerNode(1),
            new IntegerNode(2),
            new IntegerNode(3),
            new IntegerNode(4)
        ];

        int indexToTry = property.Count;

        PropertyNode node;
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => node = property[indexToTry]);
    }

    [TestMethod]
    public void ArrayNodeHandlesReturningValueFromIndex()
    {
        ArrayNode property = [
            new IntegerNode(0),
            new IntegerNode(1),
            new IntegerNode(2),
            new IntegerNode(3),
            new IntegerNode(4)
        ];

        IntegerNode node = (IntegerNode) property[1];
        int value = (int) node.Value;

        Assert.AreEqual(1, value);
    }

    [TestMethod]
    public void ArrayNodeHandlesAddsItem()
    {
        ArrayNode property = [];
        Assert.HasCount(0, property);

        property.Add(new IntegerNode(0));
        property.Add(new IntegerNode(1));
        property.Add(new IntegerNode(2));
        property.Add(new IntegerNode(3));
        property.Add(new IntegerNode(4));

        Assert.HasCount(5, property);
    }

    [TestMethod]
    public void ArrayNodeEnumeratesCorrectlyWithCorrectEnumerator()
    {
        ArrayNode property = [
                new IntegerNode(0),
                new IntegerNode(1),
                new IntegerNode(2)
            ];

        int index = 0;
        foreach (PropertyNode item in property) {
            IntegerNode node = (IntegerNode) item;
            int value = (int) node.Value;
            Assert.AreEqual(index, value);
            index++;
        }
    }

    [TestMethod]
    public void PropertyNodeConvertsToArrayNode()
    {
        PropertyNode property = new ArrayNode {
                new IntegerNode(0),
                new IntegerNode(1),
                new IntegerNode(2)
            };

        ArrayNode value = property.AsArrayNode();

        Assert.AreEqual(3, value.Count);

        IntegerNode node = (IntegerNode) value[1];
        int numberValue = (int) node.Value;
        Assert.AreEqual(1, numberValue);
    }
}

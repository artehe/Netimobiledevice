﻿using Netimobiledevice.Plist;

namespace NetimobiledeviceTest.Plist;

[TestClass]
public class BooleanNodeTests
{
    [TestMethod]
    public void BooleanNodeHandlesTrue()
    {
        BooleanNode node = new BooleanNode(true);
        bool returnedValue = node.Value;
        Assert.IsTrue(returnedValue);
    }

    [TestMethod]
    public void BooleanNodeHandlesFalse()
    {
        BooleanNode node = new BooleanNode(false);
        bool returnedValue = node.Value;
        Assert.IsFalse(returnedValue);
    }

    [TestMethod]
    public void BooleanNodeToStringReturnsNodeType()
    {
        BooleanNode value = new BooleanNode(true);
        Assert.AreEqual($"<boolean>: True", value.ToString());
    }
}

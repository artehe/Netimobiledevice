﻿using Netimobiledevice.Plist;

namespace NetimobiledeviceTest.Plist;

[TestClass]
public class DataNodeTests
{
    [TestMethod]
    public void ByteSequenceIsCorrectUsingShortArray()
    {
        byte[] initialByteArray = new byte[] { 255 };

        DataNode node = new DataNode(initialByteArray);
        Assert.AreEqual(1, node.Value.Length);

        byte[] returntedByteArray = node.Value;
        Assert.AreEqual(initialByteArray, returntedByteArray);
    }

    [TestMethod]
    public void ByteSequenceIsCorrectUsingLongArray()
    {
        List<byte> longByteArray = new List<byte>();
        for (int i = 1; i < 256; i++) {
            longByteArray.Add((byte) i);
        }
        byte[] initialByteArray = longByteArray.ToArray();

        DataNode node = new DataNode(initialByteArray);
        Assert.AreEqual(255, node.Value.Length);

        byte[] returntedByteArray = node.Value;
        Assert.AreEqual(initialByteArray, returntedByteArray);
    }

    [TestMethod]
    public void ByteSequenceIsCorrectUsingZeroByteValueAtStart()
    {
        byte[] initialByteArray = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 };

        DataNode node = new DataNode(initialByteArray);
        Assert.AreEqual(8, node.Value.Length);

        byte[] returntedByteArray = node.Value;
        Assert.AreEqual(initialByteArray, returntedByteArray);
    }

    [TestMethod]
    public void ByteSequenceIsCorrectUsingZeroByteValueInMiddle()
    {
        byte[] initialByteArray = new byte[] { 1, 2, 3, 4, 0, 3, 2, 1 };

        DataNode node = new DataNode(initialByteArray);
        Assert.AreEqual(8, node.Value.Length);

        byte[] returntedByteArray = node.Value;
        Assert.AreEqual(initialByteArray, returntedByteArray);
    }

    [TestMethod]
    public void ByteSequenceIsCorrectUsingZeroByteValueAtEnd()
    {
        byte[] initialByteArray = new byte[] { 1, 2, 3, 4, 5, 6, 7, 0 };

        DataNode node = new DataNode(initialByteArray);
        Assert.AreEqual(8, node.Value.Length);

        byte[] returntedByteArray = node.Value;
        Assert.AreEqual(initialByteArray, returntedByteArray);
    }

    [TestMethod]
    public void ToStringReturnsNodeType()
    {
        DataNode value = new DataNode(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 });
        Assert.AreEqual($"<data>: System.Byte[]", value.ToString());
    }

    [TestMethod]
    public void PropertyNodeConvertsToByteArray()
    {
        byte[] initialByteArray = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 };
        PropertyNode property = new DataNode(initialByteArray);
        byte[] returntedByteArray = ((DataNode) property).Value;
        Assert.AreEqual(initialByteArray, returntedByteArray);
    }

}

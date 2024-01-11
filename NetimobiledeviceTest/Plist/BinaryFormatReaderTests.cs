using Netimobiledevice.Plist;
using NetimobiledeviceTest.TestFiles;

namespace NetimobiledeviceTest.Plist;

[TestClass]
public class BinaryFormatReaderTests
{
    [TestMethod]
    public void ParseBinaryPlistWithSingleDictionary()
    {
        using (Stream stream = TestFileHelper.GetTestFileStream("TestFiles/GenericBinary.plist")) {
            PropertyNode node = PropertyList.Load(stream);
            Assert.IsNotNull(node);
            DictionaryNode dictionary = node.AsDictionaryNode();
            Assert.IsNotNull(dictionary);
            Assert.AreEqual(14, dictionary.Count);
        }
    }

    [TestMethod]
    public void ReadFileWithUID()
    {
        using (Stream stream = TestFileHelper.GetTestFileStream("TestFiles/UidBinary.plist")) {
            DictionaryNode root = PropertyList.Load(stream).AsDictionaryNode();

            Assert.IsNotNull(root);
            Assert.AreEqual(4, root.Count);

            DictionaryNode dict = root["$top"].AsDictionaryNode();
            Assert.IsNotNull(dict);

            UidNode uid = dict["data"].AsUidNode();
            Assert.IsNotNull(uid);

            Assert.AreEqual<ulong>(1, uid.Value);
        }
    }

    [TestMethod]
    public void ReadingFileWith16bitIntegers()
    {
        using (Stream stream = TestFileHelper.GetTestFileStream("TestFiles/UnityBinary.plist")) {
            try {
                PropertyNode node = PropertyList.Load(stream);
            }
            catch (PlistFormatException ex) {
                Assert.Fail(ex.Message);
            }
        }
    }

    [TestMethod]
    public void ReadingFileWithLargeDictionary()
    {
        using (Stream stream = TestFileHelper.GetTestFileStream("TestFiles/LargeDictionaryBinary.plist")) {
            try {
                PropertyNode node = PropertyList.Load(stream);
                DictionaryNode dictNode = node.AsDictionaryNode();
                Assert.IsNotNull(dictNode);
                Assert.AreEqual(32768, dictNode.Keys.Count);
            }
            catch (PlistFormatException ex) {
                Assert.Fail(ex.Message);
            }
        }
    }
}

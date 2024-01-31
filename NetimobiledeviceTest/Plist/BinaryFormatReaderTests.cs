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
            DictionaryNode node = PropertyList.Load(stream).AsDictionaryNode();

            Assert.AreEqual<ulong>(4, node["settings_manual_slot_count"].AsIntegerNode().Value);
            Assert.AreEqual(1.000000, node["settings_audio_master_v1"].AsRealNode().Value);
            Assert.AreEqual<ulong>(0, node["Screenmanager Is Fullscreen mode"].AsIntegerNode().Value);
            Assert.AreEqual(0.7034282088279724, node["settings_audio_ambient_v1"].AsRealNode().Value);
            Assert.AreEqual("{\"timestamp\":131326258247021180,\"locationName\":null,\"sceneName\":\"Orbit_loby\",\"shownName\":\"\",\"kind\":2,\"slotIndex\":-1}", node["chapter_7_info"].AsStringNode().Value);
            Assert.AreEqual<ulong>(1024, node["Screenmanager Resolution Width"].AsIntegerNode().Value);
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

using Netimobiledevice.Plist;
using NetimobiledeviceTest.TestFiles;

namespace NetimobiledeviceTest.Plist;

[TestClass]
public class XmlReaderTests
{
    [TestMethod]
    public void ParseXmlPlistWithSingleDictionary()
    {
        using (Stream stream = TestFileHelper.GetTestFileStream("TestFiles/GenericXml.plist")) {
            PropertyNode node = PropertyList.Load(stream);
            DictionaryNode dictionary = node.AsDictionaryNode();
            Assert.IsNotNull(dictionary);
            Assert.AreEqual(14, dictionary.Count);
        }
    }

    [TestMethod]
    public void DocumentContainingNestedCollections()
    {
        using (Stream stream = TestFileHelper.GetTestFileStream("TestFiles/DictInsideArrayXml.plist")) {
            PropertyNode node = PropertyList.Load(stream);

            Assert.IsNotNull(node);
            Assert.IsInstanceOfType<DictionaryNode>(node);

            ArrayNode array = node.AsDictionaryNode().Values.First().AsArrayNode();
            Assert.IsNotNull(array);
            Assert.AreEqual(1, array.Count);

            DictionaryNode dictionary = array[0].AsDictionaryNode();
            Assert.IsNotNull(dictionary);

            Assert.AreEqual(4, dictionary.Count);
        }
    }

    [TestMethod]
    public void DocumentContainingEmptyArray()
    {
        using (Stream stream = TestFileHelper.GetTestFileStream("TestFiles/EmptyArrayXml.plist")) {
            DictionaryNode root = PropertyList.Load(stream).AsDictionaryNode();

            Assert.IsNotNull(root);
            Assert.AreEqual(1, root.Count);

            Assert.IsInstanceOfType<DictionaryNode>(root["Entitlements"]);
            DictionaryNode dict = root["Entitlements"].AsDictionaryNode();

            ArrayNode array = dict["com.apple.developer.icloud-container-identifiers"].AsArrayNode();
            Assert.IsNotNull(array);
            Assert.AreEqual(0, array.Count);
        }
    }

    [TestMethod]
    public void ReadingFileWith16bitIntegers()
    {
        using (Stream stream = TestFileHelper.GetTestFileStream("TestFiles/UnityXml.plist")) {
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
        using (Stream stream = TestFileHelper.GetTestFileStream("TestFiles/LargeDictionaryXml.plist")) {
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

    [TestMethod]
    public void HandleEmptyDictionary()
    {
        using (Stream stream = TestFileHelper.GetTestFileStream("TestFiles/EmptyDictionaryXml.plist")) {
            try {
                DictionaryNode rootNode = PropertyList.Load(stream).AsDictionaryNode();

                Assert.IsNotNull(rootNode);
                Assert.AreEqual(4, rootNode.Count);

                // An empty dictionary in the root
                Assert.IsInstanceOfType<DictionaryNode>(rootNode["{}"]);
                DictionaryNode dict = rootNode["{}"].AsDictionaryNode();

                Assert.IsNotNull(dict);
                Assert.AreEqual(0, dict.Count);

                // an empty dictionary that is nested
                dict = rootNode["{\"foo\":{\"bar\":{\"quux\":{}}}}"].AsDictionaryNode();
                Assert.IsNotNull(dict);

                dict = dict["foo"].AsDictionaryNode();
                Assert.IsNotNull(dict);

                dict = dict["bar"].AsDictionaryNode();
                Assert.IsNotNull(dict);

                dict = dict["quux"].AsDictionaryNode();
                Assert.IsNotNull(dict);

                Assert.AreEqual(0, dict.Count);
            }
            catch (PlistFormatException ex) {
                Assert.Fail(ex.Message);
            }
        }
    }


    [TestMethod]
    public void HandleBooleans()
    {
        using (Stream stream = TestFileHelper.GetTestFileStream("TestFiles/BooleansXml.plist")) {
            try {
                DictionaryNode rootNode = PropertyList.Load(stream).AsDictionaryNode();

                Assert.IsNotNull(rootNode);
                Assert.AreEqual(2, rootNode.Count);

                // true node
                BooleanNode node = rootNode["foo"].AsBooleanNode();
                Assert.IsNotNull(node);
                Assert.IsTrue(node.Value);

                // false node
                node = rootNode["bar"].AsBooleanNode();
                Assert.IsNotNull(node);
                Assert.IsFalse(node.Value);
            }
            catch (PlistFormatException ex) {
                Assert.Fail(ex.Message);
            }
        }
    }
}


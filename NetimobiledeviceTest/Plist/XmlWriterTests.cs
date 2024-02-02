using Netimobiledevice.Plist;
using NetimobiledeviceTest.TestFiles;
using System.Text;

namespace NetimobiledeviceTest.Plist;

[TestClass]
public class XmlWriterTests
{
    [TestMethod]
    public void WhenXmlFormatIsSavedAndOpened_ThenParsedDocumentMatchesTheOriginal()
    {
        using (Stream stream = TestFileHelper.GetTestFileStream("TestFiles/UnityXml.plist")) {
            // test for <ustring> elements
            bool containsUStrings;
            using (var reader = new StreamReader(stream, Encoding.UTF8, true, 4096, true)) {
                string text = reader.ReadToEnd();
                containsUStrings = text.Contains("<ustring>");
                stream.Seek(0, SeekOrigin.Begin);
            }

            PropertyNode node = PropertyList.Load(stream);
            using (var outStream = new MemoryStream()) {
                PropertyList.Save(node, outStream, PlistFormat.Xml);

                // rewind and reload
                outStream.Seek(0, SeekOrigin.Begin);
                PropertyNode newNode = PropertyList.Load(outStream);

                // compare
                Assert.AreEqual(node.GetType().Name, newNode.GetType().Name);

                var oldDict = node as DictionaryNode;
                var newDict = newNode as DictionaryNode;

                Assert.IsNotNull(oldDict);
                Assert.IsNotNull(newDict);
                Assert.AreEqual(oldDict.Count, newDict.Count);

                foreach (string key in oldDict.Keys) {
                    Assert.IsTrue(newDict.ContainsKey(key));

                    PropertyNode oldValue = oldDict[key];
                    PropertyNode newValue = newDict[key];

                    Assert.AreEqual(oldValue.GetType().Name, newValue.GetType().Name);
                    Assert.AreEqual(oldValue, newValue);
                }

                // lastly, confirm <ustring> contents have not changed
                outStream.Seek(0, SeekOrigin.Begin);
                using (var reader = new StreamReader(outStream)) {
                    string text = reader.ReadToEnd();
                    bool outContainsUStrings = text.Contains("<ustring>");

                    Assert.AreEqual(containsUStrings, outContainsUStrings);
                }
            }
        }
    }

    [TestMethod]
    public void WhenBooleanValueIsSavedItIsCorrect()
    {
        using (var outStream = new MemoryStream()) {
            // create basic PList containing a boolean value
            DictionaryNode node = new DictionaryNode {
                { "Test", new BooleanNode(true) },
                { "Test2", new BooleanNode(false) }
            };

            // save and reset stream
            PropertyList.Save(node, outStream, PlistFormat.Xml);

            outStream.Seek(0, SeekOrigin.Begin);
            DictionaryNode newNode = PropertyList.LoadFromByteArray(outStream.ToArray()).AsDictionaryNode();

            Assert.IsNotNull(node);
            Assert.IsNotNull(newNode);
            Assert.AreEqual(node.Count, newNode.Count);

            foreach (string key in node.Keys) {
                Assert.IsTrue(newNode.ContainsKey(key));

                PropertyNode oldValue = node[key];
                PropertyNode newValue = newNode[key];

                Assert.AreEqual(oldValue.GetType().Name, newValue.GetType().Name);
                Assert.AreEqual(oldValue, newValue);
            }

            outStream.Seek(0, SeekOrigin.Begin);

            // check that boolean was written out without a space per spec (see also issue #11)
            using (var reader = new StreamReader(outStream)) {
                string contents = reader.ReadToEnd();
                Assert.IsTrue(contents.Contains("<true/>"));
                Assert.IsTrue(contents.Contains("<false/>"));
            }

        }
    }

    [TestMethod]
    public void WhenStringContainsUnicodeCorrect()
    {
        string utf16value = "😂test";

        using (var outStream = new MemoryStream()) {
            // create basic PList containing a boolean value
            DictionaryNode node = new DictionaryNode { { "Test", new StringNode(utf16value) } };

            // save and reset stream
            PropertyList.Save(node, outStream, PlistFormat.Xml);
            outStream.Seek(0, SeekOrigin.Begin);

            DictionaryNode newNode = PropertyList.LoadFromByteArray(outStream.ToArray()).AsDictionaryNode();
            Assert.AreEqual(true, newNode["Test"].AsStringNode().IsUtf16);
            Assert.AreEqual(utf16value, newNode["Test"].AsStringNode().Value);

            outStream.Seek(0, SeekOrigin.Begin);

            // check that boolean was written out without a space per spec
            using (var reader = new StreamReader(outStream)) {
                string contents = reader.ReadToEnd();
                Assert.IsTrue(contents.Contains($"<ustring>{utf16value}</ustring>"));
            }
        }
    }


    [TestMethod]
    public void ConvertsPlistDictionaryCorrectly()
    {
        string request = "StartActivity";
        int messageFilter = 65535;
        int pid = -1;

        DictionaryNode dictNode = new DictionaryNode() {
            { "Request", new StringNode(request) },
            { "MessageFilter", new IntegerNode(messageFilter) },
            { "Pid", new IntegerNode(pid) },
        };

        using (var outStream = new MemoryStream()) {
            // save and reset stream
            PropertyList.Save(dictNode, outStream, PlistFormat.Xml);
            outStream.Seek(0, SeekOrigin.Begin);

            using (var reader = new StreamReader(outStream)) {
                string contents = reader.ReadToEnd();
                Assert.IsTrue(contents.Contains($"<integer>{pid}</integer>"));
            }
        }

        byte[] plistBytes = PropertyList.SaveAsByteArray(dictNode, PlistFormat.Xml);
        DictionaryNode reReadNode = PropertyList.LoadFromByteArray(plistBytes).AsDictionaryNode();
        Assert.AreEqual(request, reReadNode["Request"].AsStringNode().Value);
        Assert.AreEqual(messageFilter, (int) reReadNode["MessageFilter"].AsIntegerNode().Value);
        Assert.AreEqual(pid, (int) reReadNode["Pid"].AsIntegerNode().Value);
    }
}

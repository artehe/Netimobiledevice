using Netimobiledevice.Remoted.Xpc;
using NetimobiledeviceTest.TestFiles;

namespace NetimobiledeviceTest.Remoted.Xpc;

[TestClass]
public class XpcWrapperTests
{
    [TestMethod]
    public void CreatesSerialisedWrapperCorrectly()
    {
        byte[] expectedData = [
            0x92, 0xb, 0xb0, 0x29, 0x1, 0x0, 0x0, 0x0, 0x14, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0,
            0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x42, 0x37, 0x13, 0x42, 0x5, 0x0, 0x0, 0x0,
            0x0, 0xf0, 0x0, 0x0, 0x4, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0
        ];

        XpcWrapper xpcWrapper = XpcWrapper.Create([], 0, false);
        byte[] data = xpcWrapper.Serialise();

        Assert.AreEqual(expectedData.Length, data.Length);
        for (int i = 0; i < data.Length; i++) {
            Assert.AreEqual(expectedData[i], data[i]);
        }
    }

    [TestMethod]
    public void CreatesSerialisedWrapperCorrectly1()
    {
        byte[] expectedData = [
            0x92, 0x0b, 0xb0, 0x29, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
        ];

        XpcWrapper xpcWrapper = new XpcWrapper {
            Flags = 0x00,
            Message = new XpcMessage() {
                Payload = null
            }
        };
        byte[] data = xpcWrapper.Serialise();

        Assert.AreEqual(expectedData.Length, data.Length);
        for (int i = 0; i < data.Length; i++) {
            Assert.AreEqual(expectedData[i], data[i]);
        }
    }

    [TestMethod]
    public void CreatesSerialisedWrapperCorrectly2()
    {
        byte[] expectedData = [
            0x92, 0x0b, 0xb0, 0x29, 0x01, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
        ];

        XpcWrapper xpcWrapper = new XpcWrapper {
            Flags = (XpcFlags) 0x0201,
            Message = new XpcMessage() {
                Payload = null
            }
        };
        byte[] data = xpcWrapper.Serialise();

        Assert.AreEqual(expectedData.Length, data.Length);
        for (int i = 0; i < data.Length; i++) {
            Assert.AreEqual(expectedData[i], data[i]);
        }
    }

    [TestMethod]
    public void CreatesDeserialisedWrapperCorrectly()
    {
        List<byte> data = [];
        using (Stream stream = TestFileHelper.GetTestFileStream("TestFiles/XpcMessage.bin")) {
            byte[] buffer = new byte[4096];
            int read;
            while ((read = stream.Read(buffer)) > 0) {
                data.AddRange(buffer.Take(read));
            }
        }

        XpcWrapper xpcWrapper = XpcWrapper.Deserialise([.. data]);
        XpcWrapper expectedXpcWrapper = new XpcWrapper {
            Flags = XpcFlags.AlwaysSet,
            Message = new XpcMessage() {
                MessageId = 0,
                Payload = new XpcPayload() {
                    Obj = new XpcDictionary()
                }
            }
        };

        Assert.AreEqual(expectedXpcWrapper.Magic, xpcWrapper.Magic);
        Assert.AreEqual(expectedXpcWrapper.Flags, xpcWrapper.Flags);
        Assert.AreEqual(expectedXpcWrapper.Message.MessageId, xpcWrapper.Message.MessageId);
        Assert.AreEqual(expectedXpcWrapper.Message.Payload.Magic, xpcWrapper.Message.Payload?.Magic);
        Assert.AreEqual(expectedXpcWrapper.Message.Payload.ProtocolVersion, xpcWrapper.Message.Payload?.ProtocolVersion);

        XpcDictionary obj = (XpcDictionary) xpcWrapper.Message.Payload!.Obj!;
        XpcDictionary expectedObj = (XpcDictionary) expectedXpcWrapper.Message.Payload!.Obj;
        Assert.AreEqual(expectedObj.Count, obj.Count);
        foreach (KeyValuePair<string, XpcObject> expectedEntry in expectedObj) {
            Assert.IsTrue(obj.ContainsKey(expectedEntry.Key));
            Assert.AreEqual(expectedEntry.Value, obj[expectedEntry.Key]);
        }
    }
}

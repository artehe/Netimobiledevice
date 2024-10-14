using Netimobiledevice.Remoted.Xpc;

namespace NetimobiledeviceTest.Remoted.Xpc
{
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

            XpcWrapper xpcWrapper = XpcWrapper.Create(new Dictionary<string, object>(), 0, false);
            byte[] data = xpcWrapper.Serialise();

            Assert.AreEqual(data.Length, expectedData.Length);
            for (int i = 0; i < data.Length; i++) {
                Assert.AreEqual(expectedData[i], data[i]);
            }
        }
    }
}

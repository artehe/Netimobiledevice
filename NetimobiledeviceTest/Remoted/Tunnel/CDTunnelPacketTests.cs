using Netimobiledevice.Remoted.Tunnel;
using Newtonsoft.Json;

namespace NetimobiledeviceTest.Remoted.Tunnel
{
    [TestClass]
    public class CDTunnelPacketTests
    {
        [TestMethod]
        public void CreatesSerialisedCDTunnelPacketCorrectly()
        {
            byte[] expectedData = [
                0x43, 0x44, 0x54, 0x75, 0x6e, 0x6e, 0x65, 0x6c, 0x00, 0x30, 0x7b, 0x22, 0x74, 0x79, 0x70, 0x65, 0x22,
                0x3a, 0x20, 0x22, 0x63, 0x6c, 0x69, 0x65, 0x6e, 0x74, 0x48, 0x61, 0x6e, 0x64, 0x73, 0x68, 0x61, 0x6b,
                0x65, 0x52, 0x65, 0x71, 0x75, 0x65, 0x73, 0x74, 0x22, 0x2c, 0x20, 0x22, 0x6d, 0x74, 0x75, 0x22, 0x3a,
                0x20, 0x31, 0x36, 0x30, 0x30, 0x30, 0x7d
            ];

            dynamic message = new Dictionary<string, object>() {
                { "type", "clientHandshakeRequest" },
                { "mtu", "16000" }
            };
            byte[] data = new CDTunnelPacket(JsonConvert.SerializeObject(message)).GetBytes();

            Assert.AreEqual(expectedData.Length, data.Length);
            for (int i = 0; i < data.Length; i++) {
                Assert.AreEqual(expectedData[i], data[i]);
            }
        }
    }
}

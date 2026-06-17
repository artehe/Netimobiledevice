using Netimobiledevice.Remoted.Bonjour;

namespace NetimobiledeviceTest.Remoted.Bonjour;

[TestClass]
public class AddressTests {

    [TestMethod]
    public void Address_FullIp_ScopesLinkLocalIpv6_WithInterfaceIndexZone() {
        Address linkLocal = new("fe80::8a66:5aff:fe72:c34", "26");
        Assert.AreEqual("fe80::8a66:5aff:fe72:c34%26", linkLocal.FullIp);

        // A global/non-link-local address has no zone and must be emitted bare.
        Address global = new("192.168.68.55", "Wi-Fi");
        Assert.AreEqual("192.168.68.55", global.FullIp);

        // An empty zone must NOT produce the invalid "fe80::...%" — the bare address is returned instead.
        Address linkLocalNoZone = new("fe80::8a66:5aff:fe72:c34", "");
        Assert.AreEqual("fe80::8a66:5aff:fe72:c34", linkLocalNoZone.FullIp);
    }
}

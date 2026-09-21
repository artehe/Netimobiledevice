using Netimobiledevice.Bonjour;

namespace NetimobiledeviceTest.Bonjour;

[TestClass]
public class MdnsBrowserTests {
    [TestMethod]
    public void Address_FullIp_ReturnsPlainIpv4() {
        Address address = new Address("192.168.1.20", "en0");
        Assert.AreEqual("192.168.1.20", address.FullIp);
    }

    [TestMethod]
    public void Address_FullIp_AddsInterfaceToIpv6LinkLocal() {
        Address address = new Address("fe80::1234", "en0");
        Assert.AreEqual("fe80::1234%en0", address.FullIp);
    }

    [TestMethod]
    public void Address_FullIp_DoesNotAddInterfaceToGlobalIpv6() {
        Address address = new Address("2001:db8::1234", "en0");
        Assert.AreEqual("2001:db8::1234", address.FullIp);
    }

    [TestMethod]
    public void ServiceInstance_HasExpectedDefaults() {
        ServiceInstance service = new ServiceInstance {
            Instance = "Device._demo._tcp.local.",
            Host = "device.local.",
            Port = 62000
        };
        Assert.AreEqual("Device._demo._tcp.local.", service.Instance);
        Assert.AreEqual("device.local.", service.Host);
        Assert.AreEqual(62000, service.Port);
        Assert.IsEmpty(service.Addresses);
        Assert.IsEmpty(service.Properties);
    }
}

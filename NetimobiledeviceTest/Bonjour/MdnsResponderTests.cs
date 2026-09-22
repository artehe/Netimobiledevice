using Netimobiledevice.Bonjour;
using System.Net.Sockets;

namespace NetimobiledeviceTest.Bonjour;

[TestClass]
public class MdnsResponderTests {
    [TestMethod]
    public void Constructor_AddsTrailingDotToServiceType() {
        MdnsResponder responder = new MdnsResponder(
            new FakeMdnsSocketFactory(),
            "_demo._tcp.local",
            "MyDevice",
            12345,
            new Dictionary<string, string>()
        );
        Assert.AreEqual("_demo._tcp.local.", responder.ServiceType);
    }

    [TestMethod]
    public void Constructor_CreatesInstanceFqdn() {
        MdnsResponder responder = new MdnsResponder(
            new FakeMdnsSocketFactory(),
            "_demo._tcp.local.",
            "MyDevice",
            12345,
            new Dictionary<string, string>()
        );
        Assert.AreEqual("MyDevice._demo._tcp.local.", responder.InstanceFqdn);
    }

    [TestMethod]
    public void Constructor_CreatesDefaultHostname() {
        MdnsResponder responder = new MdnsResponder(
            new FakeMdnsSocketFactory(),
            "_demo._tcp.local.",
            "MyDevice",
            12345,
            new Dictionary<string, string>()
        );
        Assert.AreEqual("MyDevice.local.", responder.Hostname);
    }

    [TestMethod]
    public void Constructor_AddsTrailingDotToExplicitHostname() {
        MdnsResponder responder = new MdnsResponder(
            new FakeMdnsSocketFactory(),
            "_demo._tcp.local.",
            "MyDevice",
            12345,
            new Dictionary<string, string>(),
            hostname: "host.local"
        );
        Assert.AreEqual("host.local.", responder.Hostname);
    }

    [TestMethod]
    public void Matches_ServicePtrQuery() {
        MdnsResponder responder =
            CreateResponder();

        Assert.IsTrue(
            responder.Matches(
                "_demo._tcp.local.",
                MdnsConstants.QTypePtr));
    }

    [TestMethod]
    public void Matches_ServiceAnyQuery() {
        MdnsResponder responder =
            CreateResponder();

        Assert.IsTrue(
            responder.Matches(
                "_demo._tcp.local.",
                255));
    }

    [TestMethod]
    public void Matches_InstanceSrvQuery() {
        MdnsResponder responder =
            CreateResponder();

        Assert.IsTrue(
            responder.Matches(
                "Device._demo._tcp.local.",
                MdnsConstants.QTypeSrv));
    }

    [TestMethod]
    public void Matches_InstanceTxtQuery() {
        MdnsResponder responder =
            CreateResponder();

        Assert.IsTrue(
            responder.Matches(
                "Device._demo._tcp.local.",
                MdnsConstants.QTypeTxt));
    }

    [TestMethod]
    public void Matches_HostAQuery() {
        MdnsResponder responder =
            CreateResponder();

        Assert.IsTrue(
            responder.Matches(
                "device.local.",
                MdnsConstants.QTypeA));
    }

    [TestMethod]
    public void Matches_HostAaaaQuery() {
        MdnsResponder responder =
            CreateResponder();

        Assert.IsTrue(
            responder.Matches(
                "device.local.",
                MdnsConstants.QTypeAaaa));
    }

    [TestMethod]
    public void Matches_UnrelatedQuery() {
        MdnsResponder responder =
            CreateResponder();

        Assert.IsFalse(
            responder.Matches(
                "_other._tcp.local.",
                MdnsConstants.QTypePtr));
    }

    [TestMethod]
    public void AllRecords_ProducesPtrSrvAndTxt() {
        MdnsResponder responder = new MdnsResponder(
            new FakeMdnsSocketFactory(),
            "_demo._tcp.local.",
            "Device",
            62000,
            new Dictionary<string, string> {
                ["foo"] = "bar"
            },
            hostname: "device.local.",
            addresses: [
                (AddressFamily.InterNetwork, "192.168.1.10")
            ]
        );

        (List<byte[]> Answers, List<byte[]> Additionals) = responder.AllRecords();
        Assert.HasCount(3, Answers);
        Assert.HasCount(1, Additionals);
    }

    [TestMethod]
    public void AllRecords_TtlZeroOmitsAdditionals() {
        MdnsResponder responder = new MdnsResponder(
            new FakeMdnsSocketFactory(),
            "_demo._tcp.local.",
            "Device",
            62000,
            new Dictionary<string, string>(),
            addresses: [
                (AddressFamily.InterNetwork, "192.168.1.10")
            ]
        );

        (List<byte[]> Answers, List<byte[]> Additionals) = responder.AllRecords(0);
        Assert.HasCount(3, Answers);
        Assert.IsEmpty(Additionals);
    }

    [TestMethod]
    public void BuildMessage_SetsResponseAndAuthoritativeFlags() {
        byte[] message =
            MdnsResponder.BuildMessage(
                [[1, 2, 3]],
                [[4, 5]]);

        Assert.AreEqual(
            0x84,
            message[2]);

        Assert.AreEqual(
            0x00,
            message[3]);

        // ANCOUNT = 1
        Assert.AreEqual(0, message[6]);
        Assert.AreEqual(1, message[7]);

        // ARCOUNT = 1
        Assert.AreEqual(0, message[10]);
        Assert.AreEqual(1, message[11]);
    }

    [TestMethod]
    public void AddressRecords_ContainsIpv4AndIpv6() {
        MdnsResponder responder = new MdnsResponder(
            new FakeMdnsSocketFactory(),
            "_demo._tcp.local.",
            "Device",
            62000,
            new Dictionary<string, string>(),
            hostname: "device.local.",
            addresses: [
                (AddressFamily.InterNetwork, "192.168.1.10"),
                (AddressFamily.InterNetworkV6, "2001:db8::10")
            ]
        );

        IReadOnlyList<byte[]> records = responder.AddressRecords();
        Assert.HasCount(2, records);

        List<DnsRecord> parsed = [.. records.Select(bytes => {
            int offset = 0;
            return DnsProtocol.ParseRecord(
                bytes,
                ref offset);
        })];

        Assert.Contains(x => x.Type == MdnsConstants.QTypeA && x.Address == "192.168.1.10", parsed);
        Assert.Contains(x => x.Type == MdnsConstants.QTypeAaaa && x.Address == "2001:db8::10", parsed);
    }

    private static MdnsResponder CreateResponder() {
        return new MdnsResponder(
            new FakeMdnsSocketFactory(),
            "_demo._tcp.local.",
            "Device",
            12345,
            new Dictionary<string, string>(),
            hostname: "device.local.");
    }
}

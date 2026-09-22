using Netimobiledevice.Bonjour;
using System.Net;
using System.Net.Sockets;

namespace NetimobiledeviceTest.Bonjour;

[TestClass]
public class MdnsBrowserTests {
    public TestContext TestContext { get; set; }

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

    [TestMethod]
    public async Task BrowseServiceAsync_ReturnsDiscoveredService() {
        const string service = "_remoted._tcp.local.";
        const string instance = "My iPhone._remoted._tcp.local.";
        const string host = "My-iPhone.local.";

        MdnsPacket[] packets = [
            FakeMdnsPacket.Ptr(service, instance),
            FakeMdnsPacket.Srv(instance, host, 62078),
            FakeMdnsPacket.Txt(instance, new Dictionary<string, string> {
                ["deviceid"] = "ABC123",
                ["model"] = "iPhone"
            }),
            FakeMdnsPacket.A(host, "192.168.1.50")
        ];
        FakeMdnsSocketFactory factory = new FakeMdnsSocketFactory(packets);
        FakeMdnsInterfaceResolver fakeMdnsInterfaceResolver = new FakeMdnsInterfaceResolver() {
            InterfaceName = "fakeIface"
        };

        MdnsBrowser browser = new MdnsBrowser(factory, fakeMdnsInterfaceResolver);

        List<ServiceInstance> result = await browser.BrowseServiceAsync(service, TimeSpan.FromSeconds(1), TestContext.CancellationToken);
        Assert.HasCount(1, result);

        ServiceInstance device = result[0];

        Assert.AreEqual(instance, device.Instance);
        Assert.AreEqual("My iPhone._remoted._tcp.local.", device.Instance);
        Assert.AreEqual("My-iPhone.local", device.Host);
        Assert.AreEqual(62078, device.Port);
        Assert.AreEqual("ABC123", device.Properties["deviceid"]);
        Assert.AreEqual("iPhone", device.Properties["model"]);
        Assert.HasCount(1, device.Addresses);
        Assert.AreEqual("192.168.1.50", device.Addresses[0].Ip);
    }

    [TestMethod]
    public async Task BrowseServiceAsync_ReturnsServiceWithAddress() {
        FakeMdnsSocketFactory factory =
            new FakeMdnsSocketFactory(
                FakeMdnsPacket.Ptr("_remoted._tcp.local.", "My iPhone._remoted._tcp.local."),
                FakeMdnsPacket.Srv("My iPhone._remoted._tcp.local.", "iphone.local.", 62078),
                FakeMdnsPacket.Txt("My iPhone._remoted._tcp.local.", new Dictionary<string, string> {
                    ["deviceid"] = "ABC123"
                }),
                FakeMdnsPacket.A("iphone.local.", "192.168.1.50"));

        FakeMdnsInterfaceResolver interfaces =
            new FakeMdnsInterfaceResolver {
                InterfaceName = "en0"
            };

        MdnsBrowser browser =
            new MdnsBrowser(
                factory,
                interfaces);

        List<ServiceInstance> results =
            await browser.BrowseServiceAsync(
                "_remoted._tcp.local.",
                TimeSpan.FromSeconds(1), TestContext.CancellationToken);

        Assert.HasCount(1, results);

        ServiceInstance service = results[0];

        Assert.AreEqual(
            "My iPhone._remoted._tcp.local.",
            service.Instance);

        Assert.AreEqual(
            "iphone.local",
            service.Host);

        Assert.AreEqual(
            62078,
            service.Port);

        Assert.AreEqual(
            "ABC123",
            service.Properties["deviceid"]);

        Assert.HasCount(
            1,
            service.Addresses);

        Assert.AreEqual(
            "192.168.1.50",
            service.Addresses[0].Ip);

        Assert.AreEqual(
            "en0",
            service.Addresses[0].Interface);
    }

    [TestMethod]
    public async Task BrowseServiceAsync_MapsIpv6LinkLocalInterface() {
        FakeMdnsSocketFactory factory =
            new FakeMdnsSocketFactory(
                FakeMdnsPacket.Ptr(
                    "_remoted._tcp.local.",
                    "Device._remoted._tcp.local."),

                FakeMdnsPacket.Srv(
                    "Device._remoted._tcp.local.",
                    "device.local.",
                    62078),

                FakeMdnsPacket.Aaaa(
                    "device.local.",
                    "fe80::1234"));

        FakeMdnsInterfaceResolver interfaces =
            new FakeMdnsInterfaceResolver {
                InterfaceName = "en0"
            };

        MdnsBrowser browser =
            new MdnsBrowser(
                factory,
                interfaces);

        List<ServiceInstance> results =
            await browser.BrowseServiceAsync(
                "_remoted._tcp.local.",
                TimeSpan.FromSeconds(1), TestContext.CancellationToken);

        Address address =
            results[0].Addresses.Single();

        Assert.AreEqual(
            "fe80::1234",
            address.Ip);

        Assert.AreEqual(
            "en0",
            address.Interface);

        Assert.AreEqual(
            "fe80::1234%en0",
            address.FullIp);
    }

    [TestMethod]
    public async Task BrowseServiceAsync_SendsPtrQuery() {
        FakeMdnsSocketFactory factory =
            new FakeMdnsSocketFactory();

        FakeMdnsInterfaceResolver interfaces =
            new FakeMdnsInterfaceResolver();

        MdnsBrowser browser =
            new MdnsBrowser(
                factory,
                interfaces);

        await browser.BrowseServiceAsync(
            "_remoted._tcp.local.",
            TimeSpan.FromMilliseconds(10), TestContext.CancellationToken);

        Assert.HasCount(
            1,
            factory.SocketSet.SentPackets);

        byte[] query =
            factory.SocketSet.SentPackets[0];

        List<(string Name, ushort QType)> questions =
            DnsProtocol.ParseQuestions(query);

        Assert.HasCount(1, questions);

        Assert.AreEqual(
            "_remoted._tcp.local.",
            questions[0].Name);

        Assert.AreEqual(
            MdnsConstants.QTypePtr,
            questions[0].QType);
    }

    [TestMethod]
    public async Task BrowseServiceAsync_IgnoresPtrWithoutSrv() {
        FakeMdnsSocketFactory factory =
            new FakeMdnsSocketFactory(
                FakeMdnsPacket.Ptr(
                    "_remoted._tcp.local.",
                    "Incomplete._remoted._tcp.local."));

        MdnsBrowser browser =
            new MdnsBrowser(
                factory,
                new FakeMdnsInterfaceResolver());

        List<ServiceInstance> results =
            await browser.BrowseServiceAsync(
                "_remoted._tcp.local.",
                TimeSpan.FromMilliseconds(50), TestContext.CancellationToken);
        Assert.IsEmpty(results);
    }

    [TestMethod]
    public async Task BrowseServiceAsync_ReturnsServiceWithoutAddress() {
        string instance =
            "Device._remoted._tcp.local.";

        FakeMdnsSocketFactory factory =
            new FakeMdnsSocketFactory(
                FakeMdnsPacket.Ptr(
                    "_remoted._tcp.local.",
                    instance),

                FakeMdnsPacket.Srv(
                    instance,
                    "device.local.",
                    62078));

        MdnsBrowser browser =
            new MdnsBrowser(
                factory,
                new FakeMdnsInterfaceResolver());

        List<ServiceInstance> results =
            await browser.BrowseServiceAsync(
                "_remoted._tcp.local.",
                TimeSpan.FromMilliseconds(50), TestContext.CancellationToken);

        Assert.HasCount(1, results);

        Assert.IsEmpty(
            results[0].Addresses);
    }

    [TestMethod]
    public async Task BrowseServiceAsync_DeduplicatesAddresses() {
        string instance =
            "Device._remoted._tcp.local.";

        string host =
            "device.local.";

        MdnsPacket address1 =
            FakeMdnsPacket.A(host, "192.168.1.50");

        MdnsPacket address2 =
            FakeMdnsPacket.A(host, "192.168.1.50");

        FakeMdnsSocketFactory factory =
            new FakeMdnsSocketFactory(
                FakeMdnsPacket.Ptr(
                    "_remoted._tcp.local.",
                    instance),

                FakeMdnsPacket.Srv(
                    instance,
                    host,
                    62078),

                address1,
                address2);

        MdnsBrowser browser =
            new MdnsBrowser(
                factory,
                new FakeMdnsInterfaceResolver {
                    InterfaceName = "en0"
                });

        List<ServiceInstance> results =
            await browser.BrowseServiceAsync(
                "_remoted._tcp.local.",
                TimeSpan.FromMilliseconds(50), TestContext.CancellationToken);

        Assert.HasCount(
            1,
            results[0].Addresses);
    }

    [TestMethod]
    public async Task Responder_AnswersPtrQuery() {
        FakeMdnsSocketFactory factory = new FakeMdnsSocketFactory();
        MdnsResponder responder = new MdnsResponder(
            factory,
            "_remotepairing-pairable-host._tcp.local.",
            "TestDevice",
            62078,
            new Dictionary<string, string> {
                ["deviceid"] = "ABC123"
            },
            hostname: "testdevice.local.",
            addresses: [
                (AddressFamily.InterNetwork, "192.168.1.50")
            ]
        );
        await using (responder) {

            await responder.StartAsync(TestContext.CancellationToken);
            // The responder sends an announcement when it starts.
            // Clear that so we're only examining the response to our query.
            factory.SocketSet.SentPackets.Clear();

            // Build a DNS query for the service type.
            byte[] query =
                DnsProtocol.BuildQuery(
                    "_remotepairing-pairable-host._tcp.local.",
                    MdnsConstants.QTypePtr);

            // Simulate another mDNS device sending that query.
            factory.SocketSet.InjectIncoming(
                new MdnsPacket(
                    query,
                    new IPEndPoint(
                        IPAddress.Parse("192.168.1.100"),
                        5353)));

            // Wait until the responder processes the query.
            byte[] response = await factory.SocketSet.PacketSent.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.CancellationToken);

            // Assert
            Assert.IsNotNull(response);

            List<DnsRecord> records =
                DnsProtocol.ParseMdnsMessage(response);

            // We expect:
            //
            // PTR   _remotepairing-pairable-host._tcp.local.
            //       -> TestDevice._remotepairing-pairable-host._tcp.local.
            //
            // SRV   TestDevice... -> testdevice.local.:62078
            //
            // TXT   TestDevice... -> deviceid=ABC123
            //
            // A     testdevice.local. -> 192.168.1.50

            DnsRecord ptr =
                records.Single(r =>
                    r.Type == MdnsConstants.QTypePtr);

            Assert.AreEqual(
                "_remotepairing-pairable-host._tcp.local.",
                ptr.Name);

            Assert.AreEqual(
                "TestDevice._remotepairing-pairable-host._tcp.local.",
                ptr.PtrdName);

            DnsRecord srv =
                records.Single(r =>
                    r.Type == MdnsConstants.QTypeSrv);

            Assert.AreEqual(
                "TestDevice._remotepairing-pairable-host._tcp.local.",
                srv.Name);

            Assert.AreEqual(
                "testdevice.local.",
                srv.Target);

            ushort expectedPort = 62078;
            Assert.AreEqual(expectedPort, srv.Port);

            DnsRecord txt =
                records.Single(r =>
                    r.Type == MdnsConstants.QTypeTxt);

            Assert.AreEqual(
                "ABC123",
                txt.Txt!["deviceid"]);

            DnsRecord address =
                records.Single(r =>
                    r.Type == MdnsConstants.QTypeA);

            Assert.AreEqual(
                "testdevice.local.",
                address.Name);

            Assert.AreEqual(
                "192.168.1.50",
                address.Address);
        }
    }
}

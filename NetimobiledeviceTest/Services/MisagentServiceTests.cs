using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Plist;
using Netimobiledevice.Services;
using Netimobiledevice.Services.Misagent;
using System.Text;

namespace NetimobiledeviceTest.Services;

[TestClass]
public class MisagentServiceTests {
    private Mock<LockdownServiceProvider> _lockdown = null!;
    private Mock<IServiceConnection> _connection = null!;
    private MisagentService _service = null!;

    private DictionaryNode? _capturedRequest;

    public TestContext TestContext { get; set; }

    private void CaptureRequests(PropertyNode? response = null) {
        _connection
            .Setup(x => x.SendReceivePlistAsync(
                It.IsAny<PropertyNode>(),
                It.IsAny<PlistFormat>(),
                It.IsAny<CancellationToken>()))
            .Callback<PropertyNode, PlistFormat, CancellationToken>(
                (request, _, _) => _capturedRequest = request.AsDictionaryNode()
            )
            .ReturnsAsync(response ?? SuccessResponse());
    }

    private static byte[] EmbeddedProfile(string uuid, string prefix = "CMS ENVELOPE") {
        DictionaryNode profile = Profile(uuid);

        string xml = PropertyList.SaveAsString(profile, PlistFormat.Xml);

        return Encoding.UTF8.GetBytes(prefix + xml);
    }

    private static DictionaryNode Profile(string uuid) => new() {
        { "UUID", new StringNode(uuid) }
    };

    private static DictionaryNode RequestOf(Mock<IServiceConnection> connection) {
        PropertyNode? request = null;

        // Capture the request sent to the device.
        connection.Verify(x => x.SendReceivePlistAsync(
                It.IsAny<PropertyNode>(),
                It.IsAny<PlistFormat>(),
                It.IsAny<CancellationToken>()
            ),
            Times.AtLeastOnce);

        connection
            .Setup(x => x.SendReceivePlistAsync(
                It.IsAny<PropertyNode>(),
                It.IsAny<PlistFormat>(),
                It.IsAny<CancellationToken>()
            ))
            .Callback<PropertyNode, CancellationToken>(
                (p, _) => request = p)
            .ReturnsAsync(SuccessResponse());

        Assert.IsNotNull(request);
        return request.AsDictionaryNode();
    }

    private void Respond(PropertyNode? response) {
        _connection
            .Setup(x => x.SendReceivePlistAsync(
                It.IsAny<PropertyNode>(),
                It.IsAny<PlistFormat>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
    }

    private static DictionaryNode SuccessResponse() => new()
    {
        { "Status", new IntegerNode(0) }
    };

    private static DictionaryNode SuccessResponse(string key, PropertyNode value) {
        DictionaryNode response = SuccessResponse();
        response.Add(key, value);
        return response;
    }


    [TestInitialize]
    public async Task InitializeAsync() {
        _lockdown = new Mock<LockdownServiceProvider>();
        _connection = new Mock<IServiceConnection>();

        _lockdown.SetupGet(x => x.Logger).Returns(NullLogger.Instance);
        _lockdown.SetupGet(x => x.OsVersion).Returns(new Version(17, 0));

        _lockdown.Setup(x => x.StartLockdownServiceAsync(
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(_connection.Object);

        _service = new MisagentService(_lockdown.Object);

        await _service.ConnectAsync(TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task CopyAllAsync_ParsesEmbeddedProfiles() {
        ArrayNode profiles = [];
        profiles.Add(new DataNode(EmbeddedProfile("profile-1")));
        profiles.Add(new DataNode(EmbeddedProfile(
            "profile-2", "CMS DATA BEFORE THE PLIST")));

        Respond(SuccessResponse("Payload", profiles));

        IReadOnlyList<DictionaryNode> result = await _service.CopyAllAsync(TestContext.CancellationToken);

        Assert.HasCount(2, result);
        Assert.AreEqual("profile-1", result[0]["UUID"].AsStringNode().Value);
        Assert.AreEqual("profile-2", result[1]["UUID"].AsStringNode().Value);
    }

    [TestMethod]
    public async Task CopyAllAsync_IgnoresNonDataItems() {
        ArrayNode profiles = [];
        profiles.Add(new StringNode("not a data node"));
        profiles.Add(new DataNode(EmbeddedProfile("profile-1")));

        Respond(SuccessResponse("Payload", profiles));

        IReadOnlyList<DictionaryNode> result = await _service.CopyAllAsync(TestContext.CancellationToken);

        Assert.HasCount(1, result);
        Assert.AreEqual("profile-1", result[0]["UUID"].AsStringNode().Value);
    }

    [TestMethod]
    public async Task CopyAllAsync_ReturnsEmptyListForEmptyPayload() {
        Respond(SuccessResponse("Payload", new ArrayNode()));
        IReadOnlyList<DictionaryNode> result = await _service.CopyAllAsync(TestContext.CancellationToken);
        Assert.IsEmpty(result);
    }

    [TestMethod]
    public async Task CopyAllAsync_SendsCorrectRequest() {
        CaptureRequests(SuccessResponse("Payload", new ArrayNode()));

        await _service.CopyAllAsync(TestContext.CancellationToken);

        Assert.IsNotNull(_capturedRequest);
        Assert.AreEqual("CopyAll", _capturedRequest["MessageType"].AsStringNode().Value);
        Assert.AreEqual("Provisioning", _capturedRequest["ProfileType"].AsStringNode().Value);
    }

    [TestMethod]
    public async Task CopyAllAsync_ThrowsWhenPayloadMissing() {
        Respond(SuccessResponse());
        await Assert.ThrowsExactlyAsync<MisagentException>(() => _service.CopyAllAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task CopyAllAsync_ThrowsWhenPayloadIsNotArray() {
        Respond(SuccessResponse("Payload", new StringNode("invalid")));
        await Assert.ThrowsExactlyAsync<MisagentException>(() => _service.CopyAllAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task CopyAllAsync_ThrowsWhenProfileHasNoEmbeddedXml() {
        ArrayNode profiles = [];
        profiles.Add(new DataNode(Encoding.UTF8.GetBytes("not an embedded plist")));
        Respond(SuccessResponse("Payload", profiles));
        await Assert.ThrowsExactlyAsync<MisagentException>(() => _service.CopyAllAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task CopyAllAsync_ThrowsWhenEmbeddedPlistIsNotDictionary() {
        const string xml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<plist version=\"1.0\"><string>not a dictionary</string></plist>";

        ArrayNode profiles = [];
        profiles.Add(new DataNode(Encoding.UTF8.GetBytes(xml)));

        Respond(SuccessResponse("Payload", profiles));
        await Assert.ThrowsExactlyAsync<MisagentException>(() => _service.CopyAllAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task InstallAsync_ReturnsSuccessfulResponse() {
        DictionaryNode response = SuccessResponse("Result", new StringNode("installed"));

        Respond(response);
        DictionaryNode result = await _service.InstallAsync(new DataNode([1, 2, 3]), TestContext.CancellationToken);

        Assert.AreEqual("installed", result["Result"].AsStringNode().Value);
    }

    [TestMethod]
    public async Task InstallAsync_SendsCorrectRequest() {
        DataNode profile = new([1, 2, 3, 4]);

        CaptureRequests();

        await _service.InstallAsync(profile, TestContext.CancellationToken);

        Assert.IsNotNull(_capturedRequest);
        Assert.AreEqual("Install", _capturedRequest["MessageType"].AsStringNode().Value);
        Assert.AreEqual("Provisioning", _capturedRequest["ProfileType"].AsStringNode().Value);
        Assert.AreSequenceEqual(profile.Value, _capturedRequest["Profile"].AsDataNode().Value);
    }

    [TestMethod]
    public async Task InstallAsync_ThrowsForNullProfile() {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => _service.InstallAsync(null!, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task RemoveAsync_ReturnsSuccessfulResponse() {
        Respond(SuccessResponse());
        DictionaryNode result = await _service.RemoveAsync("com.example.app", TestContext.CancellationToken);
        Assert.AreEqual<ulong>(0, result["Status"].AsIntegerNode().Value);
    }


    [TestMethod]
    public async Task RemoveAsync_SendsCorrectRequest() {
        CaptureRequests();

        await _service.RemoveAsync("com.example.app", TestContext.CancellationToken);

        Assert.IsNotNull(_capturedRequest);
        Assert.AreEqual("Remove", _capturedRequest["MessageType"].AsStringNode().Value);
        Assert.AreEqual("Provisioning", _capturedRequest["ProfileType"].AsStringNode().Value);
        Assert.AreEqual("com.example.app", _capturedRequest["ProfileID"].AsStringNode().Value);
    }

    [TestMethod]
    public async Task RemoveAsync_ThrowsForNullProfileId() {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => _service.RemoveAsync(null!, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task InstallAsync_ThrowsForNonZeroStatus() {
        Respond(new DictionaryNode {
            { "Status", new IntegerNode(1) }
        });
        await Assert.ThrowsExactlyAsync<MisagentException>(() => _service.InstallAsync(new DataNode([1]), TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task InstallAsync_ThrowsWhenStatusMissing() {
        Respond(new DictionaryNode());
        await Assert.ThrowsExactlyAsync<MisagentException>(() => _service.InstallAsync(new DataNode([1]), TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task InstallAsync_ThrowsWhenStatusIsNotInteger() {
        Respond(new DictionaryNode {
            { "Status", new StringNode("0") }
        });
        await Assert.ThrowsExactlyAsync<MisagentException>(() => _service.InstallAsync(new DataNode([1]), TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task RemoveAsync_ThrowsWhenResponseIsNull() {
        Respond(null);
        await Assert.ThrowsExactlyAsync<MisagentException>(() => _service.RemoveAsync("com.example.app", TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task CopyAllAsync_PropagatesCancellationToken() {
        using CancellationTokenSource cts = new();

        Respond(SuccessResponse("Payload", new ArrayNode()));
        await _service.CopyAllAsync(cts.Token);

        _connection.Verify(x => x.SendReceivePlistAsync(
                It.IsAny<PropertyNode>(),
                It.IsAny<PlistFormat>(),
                cts.Token
            ),
            Times.Once
        );
    }
}

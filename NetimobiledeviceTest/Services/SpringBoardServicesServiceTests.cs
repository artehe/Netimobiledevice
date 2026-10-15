using Moq;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Plist;
using Netimobiledevice.Services;
using Netimobiledevice.Services.Springboard;

namespace NetimobiledeviceTest.Services;

[TestClass]
public sealed class SpringBoardServicesServiceTests {
    private Mock<LockdownServiceProvider> _lockdown = null!;
    private Mock<IServiceConnection> _connection = null!;
    private SpringBoardServicesService _service = null!;

    private readonly List<DictionaryNode> _requests = [];

    public TestContext TestContext { get; set; } = null!;

    private DictionaryNode LastRequest {
        get {
            Assert.IsNotEmpty(_requests, "No request was sent to the device.");
            return _requests[^1];
        }
    }

    /// <summary>Configure the device to answer every request with <paramref name="response"/>, recording each request.</summary>
    private void Respond(PropertyNode? response) {
        _connection
            .Setup(x => x.SendReceivePlistAsync(
                It.IsAny<PropertyNode>(),
                It.IsAny<PlistFormat>(),
                It.IsAny<CancellationToken>()))
            .Callback<PropertyNode, PlistFormat, CancellationToken>(
                (request, _, _) => _requests.Add(request.AsDictionaryNode()))
            .ReturnsAsync(response);
    }

    private static DictionaryNode PngResponse(byte[] png) => new() {
        { "pngData", new DataNode(png) }
    };

    private static DictionaryNode OrientationResponse(long value) => new() {
        { "interfaceOrientation", new IntegerNode(value) }
    };

    private static ArrayNode SampleIconState() {
        ArrayNode dock = [new StringNode("com.apple.mobilesafari"), new StringNode("com.apple.MobileSMS")];
        ArrayNode page = [new StringNode("com.apple.Preferences")];
        return [dock, page];
    }

    /// <summary>A service that has never been connected, so <see cref="LockdownService.Service"/> is null.</summary>
    private SpringBoardServicesService DisconnectedService() => new(_lockdown.Object);

    [TestInitialize]
    public async Task InitializeAsync() {
        _requests.Clear();
        _lockdown = new Mock<LockdownServiceProvider>();
        _connection = new Mock<IServiceConnection>();

        _lockdown.Setup(x => x.StartLockdownServiceAsync(
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(_connection.Object);

        _service = new SpringBoardServicesService(_lockdown.Object);
        await _service.ConnectAsync(TestContext.CancellationToken);
    }

    [TestMethod]
    public void Constructor_ThrowsForNullLockdown() {
        Assert.ThrowsExactly<ArgumentNullException>(() => new SpringBoardServicesService(null!));
    }

    [TestMethod]
    public async Task GetHomeScreenIconMetricsAsync_ThrowsWhenNotConnected() {
        await Assert.ThrowsExactlyAsync<SpringBoardServicessException>(
            () => DisconnectedService().GetHomeScreenIconMetricsAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task GetIconPngDataAsync_ThrowsWhenNotConnected() {
        await Assert.ThrowsExactlyAsync<SpringBoardServicessException>(
            () => DisconnectedService().GetIconPngDataAsync("com.example.app", TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task GetIconStateAsync_ThrowsWhenNotConnected() {
        await Assert.ThrowsExactlyAsync<SpringBoardServicessException>(
            () => DisconnectedService().GetIconStateAsync(cancellationToken: TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task GetInterfaceOrientationAsync_ThrowsWhenNotConnected() {
        await Assert.ThrowsExactlyAsync<SpringBoardServicessException>(
            () => DisconnectedService().GetInterfaceOrientationAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task GetWallpaperInfoAsync_ThrowsWhenNotConnected() {
        await Assert.ThrowsExactlyAsync<SpringBoardServicessException>(
            () => DisconnectedService().GetWallpaperInfoAsync("Home", TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task GetWallpaperPngDataAsync_ThrowsWhenNotConnected() {
        await Assert.ThrowsExactlyAsync<SpringBoardServicessException>(
            () => DisconnectedService().GetWallpaperPngDataAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task GetWallpaperPreviewImageAsync_ThrowsWhenNotConnected() {
        await Assert.ThrowsExactlyAsync<SpringBoardServicessException>(
            () => DisconnectedService().GetWallpaperPreviewImageAsync("Home", TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task SetIconStateAsync_ThrowsWhenNotConnected() {
        await Assert.ThrowsExactlyAsync<SpringBoardServicessException>(
            () => DisconnectedService().SetIconStateAsync(SampleIconState(), TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ReloadIconStateAsync_ThrowsWhenNotConnected() {
        await Assert.ThrowsExactlyAsync<SpringBoardServicessException>(
            () => DisconnectedService().ReloadIconStateAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task GetHomeScreenIconMetricsAsync_SendsCorrectRequest() {
        Respond(new DictionaryNode());

        await _service.GetHomeScreenIconMetricsAsync(TestContext.CancellationToken);

        Assert.HasCount(1, LastRequest);
        Assert.AreEqual("getHomeScreenIconMetrics", LastRequest["command"].AsStringNode().Value);
    }

    [TestMethod]
    public async Task GetHomeScreenIconMetricsAsync_ReturnsResponseDictionary() {
        Respond(new DictionaryNode {
            { "homeScreenIconRows", new IntegerNode(6) },
            { "homeScreenIconColumns", new IntegerNode(4) },
            { "homeScreenWidth", new RealNode(390.0) },
        });

        DictionaryNode result = await _service.GetHomeScreenIconMetricsAsync(TestContext.CancellationToken);

        Assert.HasCount(3, result);
        Assert.AreEqual(6L, result["homeScreenIconRows"].AsIntegerNode().SignedValue);
        Assert.AreEqual(4L, result["homeScreenIconColumns"].AsIntegerNode().SignedValue);
        Assert.AreEqual(390.0, result["homeScreenWidth"].AsRealNode().Value);
    }

    [TestMethod]
    public async Task GetHomeScreenIconMetricsAsync_ReturnsEmptyDictionaryForNullResponse() {
        Respond(null);

        DictionaryNode result = await _service.GetHomeScreenIconMetricsAsync(TestContext.CancellationToken);

        Assert.IsEmpty(result);
    }

    [TestMethod]
    public async Task GetHomeScreenIconMetricsAsync_PropagatesCancellationToken() {
        using CancellationTokenSource cts = new();
        Respond(new DictionaryNode());

        await _service.GetHomeScreenIconMetricsAsync(cts.Token);

        _connection.Verify(x => x.SendReceivePlistAsync(It.IsAny<PropertyNode>(), It.IsAny<PlistFormat>(), cts.Token), Times.Once);
    }

    [TestMethod]
    public async Task GetIconPngDataAsync_SendsCorrectRequest() {
        Respond(PngResponse([0x89, 0x50]));

        await _service.GetIconPngDataAsync("com.apple.mobilesafari", TestContext.CancellationToken);

        Assert.HasCount(2, LastRequest);
        Assert.AreEqual("getIconPNGData", LastRequest["command"].AsStringNode().Value);
        Assert.AreEqual("com.apple.mobilesafari", LastRequest["bundleId"].AsStringNode().Value);
    }

    [TestMethod]
    public async Task GetIconPngDataAsync_ReturnsPngBytes() {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        Respond(PngResponse(png));

        DataNode result = await _service.GetIconPngDataAsync("com.example.app", TestContext.CancellationToken);

        Assert.AreSequenceEqual(png, result.Value);
    }

    [TestMethod]
    public async Task GetIconPngDataAsync_ThrowsWhenPngDataMissing() {
        Respond(new DictionaryNode { { "Error", new StringNode("UnknownBundle") } });

        SpringBoardServicessException ex = await Assert.ThrowsExactlyAsync<SpringBoardServicessException>(
            () => _service.GetIconPngDataAsync("com.example.missing", TestContext.CancellationToken));
        Assert.Contains("pngData", ex.Message);
    }

    [TestMethod]
    public async Task GetIconPngDataAsync_ThrowsWhenResponseIsNull() {
        Respond(null);

        await Assert.ThrowsExactlyAsync<SpringBoardServicessException>(
            () => _service.GetIconPngDataAsync("com.example.app", TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task GetIconPngDataAsync_PropagatesCancellationToken() {
        using CancellationTokenSource cts = new();
        Respond(PngResponse([1]));

        await _service.GetIconPngDataAsync("com.example.app", cts.Token);

        _connection.Verify(x => x.SendReceivePlistAsync(It.IsAny<PropertyNode>(), It.IsAny<PlistFormat>(), cts.Token), Times.Once);
    }

    [TestMethod]
    public async Task GetIconStateAsync_SendsDefaultFormatVersion() {
        Respond(new ArrayNode());

        await _service.GetIconStateAsync(cancellationToken: TestContext.CancellationToken);

        Assert.HasCount(2, LastRequest);
        Assert.AreEqual("getIconState", LastRequest["command"].AsStringNode().Value);
        Assert.AreEqual("2", LastRequest["formatVersion"].AsStringNode().Value);
    }

    [TestMethod]
    public async Task GetIconStateAsync_SendsCustomFormatVersion() {
        Respond(new ArrayNode());

        await _service.GetIconStateAsync("1", TestContext.CancellationToken);

        Assert.AreEqual("1", LastRequest["formatVersion"].AsStringNode().Value);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public async Task GetIconStateAsync_OmitsFormatVersionWhenNullOrEmpty(string? formatVersion) {
        Respond(new ArrayNode());

        await _service.GetIconStateAsync(formatVersion, TestContext.CancellationToken);

        Assert.HasCount(1, LastRequest);
        Assert.IsFalse(LastRequest.ContainsKey("formatVersion"));
        Assert.AreEqual("getIconState", LastRequest["command"].AsStringNode().Value);
    }

    [TestMethod]
    public async Task GetIconStateAsync_ReturnsResponseArray() {
        Respond(SampleIconState());

        ArrayNode result = await _service.GetIconStateAsync(cancellationToken: TestContext.CancellationToken);

        Assert.HasCount(2, result);
        Assert.AreEqual("com.apple.mobilesafari", result[0].AsArrayNode()[0].AsStringNode().Value);
        Assert.AreEqual("com.apple.Preferences", result[1].AsArrayNode()[0].AsStringNode().Value);
    }

    [TestMethod]
    public async Task GetIconStateAsync_ReturnsEmptyArrayForNullResponse() {
        Respond(null);

        ArrayNode result = await _service.GetIconStateAsync(cancellationToken: TestContext.CancellationToken);

        Assert.IsEmpty(result);
    }

    [TestMethod]
    public async Task GetIconStateAsync_PropagatesCancellationToken() {
        using CancellationTokenSource cts = new();
        Respond(new ArrayNode());

        await _service.GetIconStateAsync(cancellationToken: cts.Token);

        _connection.Verify(x => x.SendReceivePlistAsync(It.IsAny<PropertyNode>(), It.IsAny<PlistFormat>(), cts.Token), Times.Once);
    }

    [TestMethod]
    public async Task GetInterfaceOrientationAsync_SendsCorrectRequest() {
        Respond(OrientationResponse(1));

        await _service.GetInterfaceOrientationAsync(TestContext.CancellationToken);

        Assert.HasCount(1, LastRequest);
        Assert.AreEqual("getInterfaceOrientation", LastRequest["command"].AsStringNode().Value);
    }

    [TestMethod]
    [DataRow(0L, ScreenOrientation.Unknown)]
    [DataRow(1L, ScreenOrientation.Portrait)]
    [DataRow(2L, ScreenOrientation.PortraitUpsideDown)]
    [DataRow(3L, ScreenOrientation.LandscapeRight)]
    [DataRow(4L, ScreenOrientation.LandscapeLeft)]
    public async Task GetInterfaceOrientationAsync_MapsValueToEnum(long value, ScreenOrientation expected) {
        Respond(OrientationResponse(value));

        ScreenOrientation result = await _service.GetInterfaceOrientationAsync(TestContext.CancellationToken);

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(5L)]
    [DataRow(-1L)]
    [DataRow(99L)]
    public async Task GetInterfaceOrientationAsync_ThrowsForUndefinedValue(long value) {
        Respond(OrientationResponse(value));

        await Assert.ThrowsExactlyAsync<SpringBoardServicessException>(
            () => _service.GetInterfaceOrientationAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task GetInterfaceOrientationAsync_ThrowsWhenKeyMissing() {
        Respond(new DictionaryNode());

        SpringBoardServicessException ex = await Assert.ThrowsExactlyAsync<SpringBoardServicessException>(
            () => _service.GetInterfaceOrientationAsync(TestContext.CancellationToken));
        Assert.Contains("interfaceOrientation", ex.Message);
    }

    [TestMethod]
    public async Task GetInterfaceOrientationAsync_ThrowsWhenResponseIsNull() {
        Respond(null);

        await Assert.ThrowsExactlyAsync<SpringBoardServicessException>(
            () => _service.GetInterfaceOrientationAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task GetWallpaperInfoAsync_SendsCorrectRequest() {
        Respond(new DictionaryNode());

        await _service.GetWallpaperInfoAsync("HomeScreen", TestContext.CancellationToken);

        Assert.HasCount(2, LastRequest);
        Assert.AreEqual("getWallpaperInfo", LastRequest["command"].AsStringNode().Value);
        Assert.AreEqual("HomeScreen", LastRequest["wallpaperName"].AsStringNode().Value);
    }

    [TestMethod]
    public async Task GetWallpaperInfoAsync_ReturnsResponseDictionary() {
        Respond(new DictionaryNode {
            { "width", new IntegerNode(1170) },
            { "height", new IntegerNode(2532) },
        });

        DictionaryNode result = await _service.GetWallpaperInfoAsync("HomeScreen", TestContext.CancellationToken);

        Assert.AreEqual(1170L, result["width"].AsIntegerNode().SignedValue);
        Assert.AreEqual(2532L, result["height"].AsIntegerNode().SignedValue);
    }

    [TestMethod]
    public async Task GetWallpaperInfoAsync_ReturnsEmptyDictionaryForNullResponse() {
        Respond(null);

        DictionaryNode result = await _service.GetWallpaperInfoAsync("HomeScreen", TestContext.CancellationToken);

        Assert.IsEmpty(result);
    }

    [TestMethod]
    public async Task GetWallpaperPngDataAsync_SendsCorrectRequest() {
        Respond(PngResponse([1]));

        await _service.GetWallpaperPngDataAsync(TestContext.CancellationToken);

        Assert.HasCount(1, LastRequest);
        Assert.AreEqual("getHomeScreenWallpaperPNGData", LastRequest["command"].AsStringNode().Value);
    }

    [TestMethod]
    public async Task GetWallpaperPngDataAsync_ReturnsPngBytes() {
        byte[] png = [0x89, 0x50, 0x4E, 0x47];
        Respond(PngResponse(png));

        DataNode result = await _service.GetWallpaperPngDataAsync(TestContext.CancellationToken);

        Assert.AreSequenceEqual(png, result.Value);
    }

    [TestMethod]
    public async Task GetWallpaperPngDataAsync_ThrowsWhenPngDataMissing() {
        Respond(new DictionaryNode());

        await Assert.ThrowsExactlyAsync<SpringBoardServicessException>(
            () => _service.GetWallpaperPngDataAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task GetWallpaperPreviewImageAsync_SendsCorrectRequest() {
        Respond(PngResponse([1]));

        await _service.GetWallpaperPreviewImageAsync("LockScreen", TestContext.CancellationToken);

        Assert.HasCount(2, LastRequest);
        Assert.AreEqual("getWallpaperPreviewImage", LastRequest["command"].AsStringNode().Value);
        Assert.AreEqual("LockScreen", LastRequest["wallpaperName"].AsStringNode().Value);
    }

    [TestMethod]
    public async Task GetWallpaperPreviewImageAsync_ReturnsPngBytes() {
        byte[] png = [9, 8, 7, 6];
        Respond(PngResponse(png));

        DataNode result = await _service.GetWallpaperPreviewImageAsync("LockScreen", TestContext.CancellationToken);

        Assert.AreSequenceEqual(png, result.Value);
    }

    [TestMethod]
    public async Task GetWallpaperPreviewImageAsync_ThrowsWhenPngDataMissing() {
        Respond(new DictionaryNode());

        await Assert.ThrowsExactlyAsync<SpringBoardServicessException>(
            () => _service.GetWallpaperPreviewImageAsync("LockScreen", TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task SetIconStateAsync_SendsCorrectRequest() {
        ArrayNode state = SampleIconState();
        Respond(null);

        await _service.SetIconStateAsync(state, TestContext.CancellationToken);

        Assert.HasCount(2, LastRequest);
        Assert.AreEqual("setIconState", LastRequest["command"].AsStringNode().Value);
        Assert.AreSame(state, LastRequest["iconState"]);
    }

    [TestMethod]
    public async Task SetIconStateAsync_SendsEmptyLayoutWhenNull() {
        Respond(null);

        await _service.SetIconStateAsync(null, TestContext.CancellationToken);

        Assert.IsEmpty(LastRequest["iconState"].AsArrayNode());
    }

    [TestMethod]
    public async Task SetIconStateAsync_PropagatesCancellationToken() {
        using CancellationTokenSource cts = new();
        Respond(null);

        await _service.SetIconStateAsync(SampleIconState(), cts.Token);

        _connection.Verify(x => x.SendReceivePlistAsync(It.IsAny<PropertyNode>(), It.IsAny<PlistFormat>(), cts.Token), Times.Once);
    }

    [TestMethod]
    public async Task ReloadIconStateAsync_ReadsThenWritesSameState() {
        ArrayNode state = SampleIconState();
        Respond(state);

        await _service.ReloadIconStateAsync(TestContext.CancellationToken);

        Assert.HasCount(2, _requests);
        Assert.AreEqual("getIconState", _requests[0]["command"].AsStringNode().Value);
        Assert.AreEqual("2", _requests[0]["formatVersion"].AsStringNode().Value);
        Assert.AreEqual("setIconState", _requests[1]["command"].AsStringNode().Value);
        Assert.AreSame(state, _requests[1]["iconState"]);
    }

    [TestMethod]
    public async Task ReloadIconStateAsync_WritesEmptyLayoutWhenReadReturnsNull() {
        Respond(null);

        await _service.ReloadIconStateAsync(TestContext.CancellationToken);

        Assert.HasCount(2, _requests);
        Assert.IsEmpty(_requests[1]["iconState"].AsArrayNode());
    }

    [TestMethod]
    public async Task ReloadIconStateAsync_PropagatesCancellationTokenToBothCalls() {
        using CancellationTokenSource cts = new();
        Respond(new ArrayNode());

        await _service.ReloadIconStateAsync(cts.Token);

        _connection.Verify(x => x.SendReceivePlistAsync(It.IsAny<PropertyNode>(), It.IsAny<PlistFormat>(), cts.Token), Times.Exactly(2));
    }

    [TestMethod]
    public async Task ReloadIconStateAsync_DoesNotWriteWhenReadIsCancelled() {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();
        _connection
            .Setup(x => x.SendReceivePlistAsync(It.IsAny<PropertyNode>(), It.IsAny<PlistFormat>(), It.IsAny<CancellationToken>()))
            .Callback<PropertyNode, PlistFormat, CancellationToken>((request, _, _) => _requests.Add(request.AsDictionaryNode()))
            .Returns<PropertyNode, PlistFormat, CancellationToken>((_, _, ct) => ValueTask.FromCanceled<PropertyNode?>(ct));

        await Assert.ThrowsAsync<OperationCanceledException>(() => _service.ReloadIconStateAsync(cts.Token));

        Assert.HasCount(1, _requests);
        Assert.AreEqual("getIconState", _requests[0]["command"].AsStringNode().Value);
    }
}

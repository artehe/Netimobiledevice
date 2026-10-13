using Microsoft.Extensions.Logging;
using Moq;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Plist;
using Netimobiledevice.Services;

namespace NetimobiledeviceTest.Services;

[TestClass]
public class HeartbeatServiceTests {
    private Mock<LockdownServiceProvider> _lockdown = null!;
    private Mock<IServiceConnection> _connection = null!;
    private Mock<ILogger> _logger = null!;

    public TestContext TestContext { get; set; }

    private HeartbeatService CreateService() => new(_lockdown.Object, _logger.Object);

    [TestInitialize]
    public void Initialize() {
        _logger = new Mock<ILogger>();
        _connection = new Mock<IServiceConnection>();

        _lockdown = new Mock<LockdownServiceProvider>();

        _lockdown.SetupGet(x => x.Logger).Returns(_logger.Object);
        _lockdown.SetupGet(x => x.OsVersion).Returns(new Version(17, 0));

        _lockdown
            .Setup(x => x.GetValue(It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns((PropertyNode?) null);

        _lockdown
            .Setup(x => x.GetValueAsync(It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync((PropertyNode?) null);

        _lockdown
            .Setup(x => x.StartLockdownServiceAsync(
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(_connection.Object);

        _lockdown
            .Setup(x => x.StartLockdownService(
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()))
            .Returns(_connection.Object);

        _connection
            .Setup(x => x.DisposeAsync())
            .Returns(ValueTask.CompletedTask);
    }

    [TestMethod]
    public async Task StartAsync_ZeroInterval_DoesNotSendPolo() {
        // Arrange
        await using (HeartbeatService service = CreateService()) {
            await service.ConnectAsync(TestContext.CancellationToken);

            _connection
                .Setup(x => x.ReceivePlistAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync((PropertyNode?) null);

            // Act
            await service.StartAsync(TimeSpan.Zero, TestContext.CancellationToken);

            // Assert
            _connection.Verify(x => x.ReceivePlistAsync(
                It.IsAny<CancellationToken>()), Times.Once
            );

            _connection.Verify(x => x.SendPlistAsync(
                It.IsAny<PropertyNode>(),
                It.IsAny<PlistFormat>(),
                It.IsAny<CancellationToken>()),
                Times.Never
            );

            _connection.Verify(x => x.DisposeAsync(), Times.Once);
        }
    }

    [TestMethod]
    public async Task StartAsync_WhenReceiveReturnsNull_DoesNotThrow() {
        // Arrange
        await using (HeartbeatService service = CreateService()) {
            await service.ConnectAsync(TestContext.CancellationToken);

            _connection
                .Setup(x => x.ReceivePlistAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync((PropertyNode?) null);

            // Act
            await service.StartAsync(TimeSpan.Zero, TestContext.CancellationToken);

            // Assert
            _connection.Verify(x => x.DisposeAsync(), Times.Once);
        }
    }

    [TestMethod]
    public async Task StartAsync_PassesCancellationTokenToReceive() {
        // Arrange
        using (CancellationTokenSource cts = new CancellationTokenSource()) {
            await using (HeartbeatService service = CreateService()) {
                await service.ConnectAsync(cts.Token);

                _connection
                    .Setup(x => x.ReceivePlistAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync((PropertyNode?) null);

                // Act
                await service.StartAsync(TimeSpan.Zero, cts.Token);

                // Assert
                _connection.Verify(x => x.ReceivePlistAsync(cts.Token), Times.Once);
            }
        }
    }

    [TestMethod]
    public async Task StartAsync_WhenReceiveThrows_DisposesConnection() {
        // Arrange
        await using (HeartbeatService service = CreateService()) {
            await service.ConnectAsync(TestContext.CancellationToken);
            InvalidOperationException expected = new InvalidOperationException("Receive failed");

            _connection
                .Setup(x => x.ReceivePlistAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(expected);

            // Act
            InvalidOperationException actual = await Assert
                .ThrowsExactlyAsync<InvalidOperationException>(() => service.StartAsync(cancellationToken: TestContext.CancellationToken));

            // Assert
            Assert.AreSame(expected, actual);
            _connection.Verify(x => x.DisposeAsync(), Times.Once);
        }
    }

    [TestMethod]
    public async Task StartAsync_WhenProviderReturnsConnection_StartsExactlyOnce() {
        // Arrange
        await using (HeartbeatService service = CreateService()) {
            await service.ConnectAsync(TestContext.CancellationToken);

            _connection
            .Setup(x => x.ReceivePlistAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((PropertyNode?) null);

            // Act
            await service.StartAsync(TimeSpan.Zero, TestContext.CancellationToken);

            // Assert
            _lockdown.Verify(x =>
                x.StartLockdownServiceAsync(
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }
    }

    [TestMethod]
    public async Task StartAsync_SendsPoloAfterReceivingResponse() {
        // Arrange
        await using (HeartbeatService service = CreateService()) {
            await service.ConnectAsync(TestContext.CancellationToken);

            int receiveCount = 0;
            PropertyNode? sentMessage = null;
            PlistFormat? sentFormat = null;

            _connection
                .Setup(x => x.ReceivePlistAsync(It.IsAny<CancellationToken>()))
                .Returns<CancellationToken>(async ct => {
                    if (Interlocked.Increment(ref receiveCount) == 1) {
                        return new DictionaryNode();
                    }

                    await Task.Delay(100, ct);
                    return new DictionaryNode();
                });

            _connection
                .Setup(x => x.SendPlistAsync(
                    It.IsAny<PropertyNode>(),
                    It.IsAny<PlistFormat>(),
                    It.IsAny<CancellationToken>()))
                .Callback<PropertyNode, PlistFormat, CancellationToken>(
                    (plist, format, _) => {
                        sentMessage = plist;
                        sentFormat = format;
                    })
                .Returns(ValueTask.CompletedTask);

            // Act
            await service.StartAsync(TimeSpan.FromMilliseconds(20), TestContext.CancellationToken);

            // Assert
            Assert.IsNotNull(sentMessage);
            Assert.IsInstanceOfType<DictionaryNode>(sentMessage);

            DictionaryNode dictionary = (DictionaryNode) sentMessage;
            Assert.IsInstanceOfType<StringNode>(dictionary["Command"]);
            Assert.AreEqual("Polo", ((StringNode) dictionary["Command"]).Value);
            Assert.AreEqual(PlistFormat.Xml, sentFormat);
        }
    }

    [TestMethod]
    public async Task StartAsync_WhenSendThrows_DisposesConnection() {
        // Arrange
        await using (HeartbeatService service = CreateService()) {
            await service.ConnectAsync(TestContext.CancellationToken);

            _connection
            .Setup(x => x.ReceivePlistAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DictionaryNode());

            InvalidOperationException expected = new InvalidOperationException("Send failed");

            _connection
                .Setup(x => x.SendPlistAsync(
                    It.IsAny<PropertyNode>(),
                    It.IsAny<PlistFormat>(),
                    It.IsAny<CancellationToken>()))
                .Throws(expected);

            // Act
            InvalidOperationException actual = await Assert
                .ThrowsExactlyAsync<InvalidOperationException>(
                    () => service.StartAsync(TimeSpan.FromSeconds(10), TestContext.CancellationToken)
                );

            // Assert
            Assert.AreSame(expected, actual);
            _connection.Verify(x => x.DisposeAsync(), Times.Once);
        }
    }
}

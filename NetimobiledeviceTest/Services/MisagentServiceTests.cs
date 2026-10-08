using Netimobiledevice.Plist;
using Netimobiledevice.Services;
using Netimobiledevice.Services.Misagent;
using NetimobiledeviceTest.TestProviders;
using System.Text;

namespace NetimobiledeviceTest.Services;

[TestClass]
public class MisagentServiceTests {
    public TestContext TestContext { get; set; }

    [TestMethod]
    public async Task RemoveAsync_WhenProfileIdIsNull_ThrowsArgumentNullException() {
        DictionaryNode response = new() {
            ["Status"] = new IntegerNode(0)
        };
        await using (TestServiceConnection server = await TestServiceConnection.StartAsync(response)) {
            TestLockdownProvider provider = new(server.Connection);
            await using (MisagentService service = new(provider)) {
                await Assert.ThrowsExactlyAsync<ArgumentNullException>(
                    () => service.RemoveAsync(null!, TestContext.CancellationToken)
                );
            }
        }
    }

    [TestMethod]
    public async Task RemoveAsync_SendsExpectedRequest() {
        DictionaryNode response = new() {
            ["Status"] = new IntegerNode(0)
        };
        await using (TestServiceConnection server = await TestServiceConnection.StartAsync(response)) {
            TestLockdownProvider provider = new(server.Connection);
            await using (MisagentService service = new(provider)) {
                await service.ConnectAsync(TestContext.CancellationToken);
                await service.RemoveAsync("com.example.test", TestContext.CancellationToken);

                DictionaryNode request = server.ReceivedRequest!;
                Assert.AreEqual("Remove", request["MessageType"].AsStringNode().Value);
                Assert.AreEqual("com.example.test", request["ProfileID"].AsStringNode().Value);
                Assert.AreEqual("Provisioning", request["ProfileType"].AsStringNode().Value);
            }
        }
    }

    [TestMethod]
    public async Task InstallAsync_WhenProfileIsNull_ThrowsArgumentNullException() {
        DictionaryNode response = new() {
            ["Status"] = new IntegerNode(0)
        };
        await using (TestServiceConnection server = await TestServiceConnection.StartAsync(response)) {
            TestLockdownProvider provider = new(server.Connection);
            await using (MisagentService service = new(provider)) {
                await Assert.ThrowsExactlyAsync<ArgumentNullException>(
                    () => service.InstallAsync(null!, TestContext.CancellationToken)
                );
            }
        }
    }

    [TestMethod]
    public async Task InstallAsync_SendsExpectedRequest() {
        DataNode profile = new(Encoding.UTF8.GetBytes("fake provisioning profile"));
        DictionaryNode response = new() {
            ["Status"] = new IntegerNode(0)
        };
        await using (TestServiceConnection server = await TestServiceConnection.StartAsync(response)) {
            TestLockdownProvider provider = new(server.Connection);
            await using (MisagentService service = new(provider)) {
                await service.ConnectAsync(TestContext.CancellationToken);
                await service.InstallAsync(profile, TestContext.CancellationToken);

                DictionaryNode request = server.ReceivedRequest!;
                Assert.AreEqual("Install", request["MessageType"].AsStringNode().Value);
                Assert.AreEqual("Provisioning", request["ProfileType"].AsStringNode().Value);
                Assert.AreSequenceEqual(profile.Value, request["Profile"].AsDataNode().Value);
            }
        }
    }

    [TestMethod]
    public async Task CopyAllAsync_WhenEmbeddedPlistIsNotDictionary_ThrowsMisagentException() {
        const string profileXml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <plist version="1.0">
            <array>
                <string>not a dictionary</string>
            </array>
            </plist>
            """;
        DictionaryNode response = new() {
            ["Status"] = new IntegerNode(0),
            ["Payload"] = new ArrayNode {
                new DataNode(Encoding.UTF8.GetBytes(profileXml))
            }
        };
        await using (TestServiceConnection server = await TestServiceConnection.StartAsync(response)) {
            TestLockdownProvider provider = new(server.Connection);
            await using (MisagentService service = new(provider)) {
                await service.ConnectAsync(TestContext.CancellationToken);

                MisagentException exception = await Assert.ThrowsExactlyAsync<MisagentException>(
                        () => service.CopyAllAsync(TestContext.CancellationToken)
                );
                Assert.Contains(
                    "Embedded provisioning profile plist is not a dictionary",
                    exception.Message
                );
            }
        }
    }

    [TestMethod]
    public async Task CopyAllAsync_WhenProfileDoesNotContainXml_ThrowsMisagentException() {
        DictionaryNode response = new() {
            ["Status"] = new IntegerNode(0),
            ["Payload"] = new ArrayNode {
                new DataNode(Encoding.UTF8.GetBytes("this is not a plist"))
            }
        };
        await using (TestServiceConnection server = await TestServiceConnection.StartAsync(response)) {
            TestLockdownProvider provider = new(server.Connection);
            await using (MisagentService service = new(provider)) {
                await service.ConnectAsync(TestContext.CancellationToken);

                MisagentException exception = await Assert.ThrowsExactlyAsync<MisagentException>(
                        () => service.CopyAllAsync(TestContext.CancellationToken)
                );
                Assert.Contains(
                    "does not contain an embedded XML plist",
                    exception.Message
                );
            }
        }
    }

    [TestMethod]
    public async Task CopyAllAsync_WhenPayloadContainsNonDataNode_SkipsIt() {
        DictionaryNode response = new() {
            ["Status"] = new IntegerNode(0),
            ["Payload"] = new ArrayNode {
                new StringNode("not a profile")
            }
        };
        await using (TestServiceConnection server = await TestServiceConnection.StartAsync(response)) {
            TestLockdownProvider provider = new(server.Connection);
            await using (MisagentService service = new(provider)) {
                await service.ConnectAsync(TestContext.CancellationToken);

                IReadOnlyList<DictionaryNode> profiles = await service.CopyAllAsync(TestContext.CancellationToken);
                Assert.IsEmpty(profiles);
            }
        }
    }

    [TestMethod]
    public async Task CopyAllAsync_WhenPayloadIsMissing_ThrowsMisagentException() {
        DictionaryNode response = new() {
            ["Status"] = new IntegerNode(0)
        };
        await using (TestServiceConnection server = await TestServiceConnection.StartAsync(response)) {
            TestLockdownProvider provider = new(server.Connection);
            await using (MisagentService service = new(provider)) {
                await service.ConnectAsync(TestContext.CancellationToken);

                MisagentException exception = await Assert.ThrowsExactlyAsync<MisagentException>(
                    () => service.CopyAllAsync(TestContext.CancellationToken)
                );
                Assert.Contains("invalid payload", exception.Message);
            }
        }
    }

    [TestMethod]
    public async Task CopyAllAsync_WhenStatusIsMissing_ThrowsMisagentException() {
        DictionaryNode response = new() {
            ["Payload"] = new ArrayNode()
        };
        await using (TestServiceConnection server = await TestServiceConnection.StartAsync(response)) {
            TestLockdownProvider provider = new(server.Connection);
            await using (MisagentService service = new(provider)) {
                await Assert.ThrowsExactlyAsync<MisagentException>(
                    () => service.CopyAllAsync(TestContext.CancellationToken)
                );
            }
        }
    }

    [TestMethod]
    public async Task CopyAllAsync_WhenStatusIsNonZero_ThrowsMisagentException() {
        DictionaryNode response = new() {
            ["Status"] = new IntegerNode(42)
        };
        await using (TestServiceConnection server = await TestServiceConnection.StartAsync(response)) {
            TestLockdownProvider provider = new(server.Connection);
            await using (MisagentService service = new(provider)) {
                await service.ConnectAsync(TestContext.CancellationToken);

                MisagentException exception = await Assert.ThrowsExactlyAsync<MisagentException>(
                    () => service.CopyAllAsync(TestContext.CancellationToken)
                );
                Assert.Contains("42", exception.Message);
            }
        }
    }

    [TestMethod]
    public async Task CopyAllAsync_SendsExpectedRequest_AndParsesProfile() {
        const string profileXml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN"
              "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
            <plist version="1.0">
            <dict>
                <key>Name</key>
                <string>Test Profile</string>
                <key>UUID</key>
                <string>12345678-1234-1234-1234-123456789ABC</string>
            </dict>
            </plist>
            """;
        byte[] embeddedProfile = Encoding.UTF8.GetBytes(
            "CMS HEADER\r\n" +
            profileXml +
            "\r\nCMS TRAILER"
        );
        DictionaryNode response = new() {
            ["Status"] = new IntegerNode(0),
            ["Payload"] = new ArrayNode {
                new DataNode(embeddedProfile)
            }
        };

        await using (TestServiceConnection server = await TestServiceConnection.StartAsync(response)) {
            TestLockdownProvider provider = new(server.Connection);
            await using (MisagentService service = new(provider)) {
                await service.ConnectAsync(TestContext.CancellationToken);

                IReadOnlyList<DictionaryNode> profiles = await service.CopyAllAsync(TestContext.CancellationToken);
                Assert.HasCount(1, profiles);

                DictionaryNode profile = profiles[0];
                Assert.AreEqual("Test Profile", profile["Name"].AsStringNode().Value);
                Assert.AreEqual("12345678-1234-1234-1234-123456789ABC", profile["UUID"].AsStringNode().Value);

                DictionaryNode request = server.ReceivedRequest!;
                Assert.AreEqual("CopyAll", request["MessageType"].AsStringNode().Value);
                Assert.AreEqual("Provisioning", request["ProfileType"].AsStringNode().Value);
            }
        }
    }
}

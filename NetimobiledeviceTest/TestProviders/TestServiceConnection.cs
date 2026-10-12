using Netimobiledevice.Lockdown;
using Netimobiledevice.Plist;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Reflection;

namespace NetimobiledeviceTest.TestProviders;

internal sealed class TestServiceConnection : IAsyncDisposable {
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();

    private TestServiceConnection(TcpListener listener, ServiceConnection connection) {
        _listener = listener;
        Connection = connection;
    }

    public ServiceConnection Connection { get; }

    public DictionaryNode? ReceivedRequest { get; private set; }

    public CancellationToken ReceivedCancellationToken { get; private set; }

    public static async Task<TestServiceConnection> StartAsync(DictionaryNode response) {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();

        int port = ((IPEndPoint) listener.LocalEndpoint).Port;

        Task<ServiceConnection> connectionTask = CreateServiceConnectionAsync(port);

        TcpClient client = await listener.AcceptTcpClientAsync();

        ServiceConnection connection = await connectionTask;

        TestServiceConnection result = new(listener, connection);

        _ = result.HandleClientAsync(client, response, result._cts.Token);
        return result;
    }

    private static async Task<ServiceConnection> CreateServiceConnectionAsync(int port) {
        MethodInfo? method = typeof(ServiceConnection).GetMethod(
            "CreateUsingTcpAsync",
            BindingFlags.Static | BindingFlags.NonPublic
        );
        Assert.IsNotNull(method);

        object? result = method.Invoke(
            null,
            [
                "127.0.0.1",
                (ushort) port,
                10_000,
                null
            ]
        );
        Assert.IsNotNull(result);
        return await (Task<ServiceConnection>) result;
    }

    private async Task HandleClientAsync(
        TcpClient client,
        DictionaryNode response,
        CancellationToken cancellationToken
    ) {
        await using (NetworkStream stream = client.GetStream()) {
            try {
                byte[] lengthBytes = await ReadExactlyAsync(
                    stream,
                    sizeof(int),
                    cancellationToken
                );
                int length = BinaryPrimitives.ReadInt32BigEndian(lengthBytes);
                byte[] requestBytes = await ReadExactlyAsync(
                    stream,
                    length,
                    cancellationToken
                );

                ReceivedRequest = PropertyList.LoadFromByteArray(requestBytes).AsDictionaryNode();

                byte[] responseBytes = PropertyList.SaveAsByteArray(response, PlistFormat.Xml);
                byte[] responseLength = new byte[sizeof(int)];

                BinaryPrimitives.WriteInt32BigEndian(responseLength, responseBytes.Length);

                await stream.WriteAsync(responseLength, cancellationToken);
                await stream.WriteAsync(responseBytes, cancellationToken);
            }
            catch (OperationCanceledException) {
                // Ignore this exception
            }
            finally {
                client.Dispose();
            }
        }
    }

    private static async Task<byte[]> ReadExactlyAsync(
        Stream stream,
        int length,
        CancellationToken cancellationToken
    ) {
        byte[] buffer = new byte[length];
        int offset = 0;
        while (offset < length) {
            int read = await stream.ReadAsync(
                buffer.AsMemory(offset, length - offset),
                cancellationToken
            );
            if (read == 0) {
                throw new EndOfStreamException();
            }
            offset += read;
        }
        return buffer;
    }

    public async ValueTask DisposeAsync() {
        _cts.Cancel();
        await Connection.DisposeAsync();
        _listener.Stop();
        _cts.Dispose();
    }
}

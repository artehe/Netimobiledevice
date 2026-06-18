using Netimobiledevice.EndianBitConversion;
using Netimobiledevice.Remoted.Xpc;
using Netimobiledevice.Serialisation;
using System;
using System.Buffers.Binary;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Remoted.Tunnel;

public class RemotePairingTunnelService : RemotePairingProtocol {
    private const int TIMEOUT = 1;
    private const string REPAIRING_PACKET_MAGIC_STRING = "RPPairing";

    private TcpClient? _client;
    private readonly string _remoteIdentifier;
    private NetworkStream? _stream;

    public string Hostname { get; }
    public int Port { get; }
    public override string RemoteIdentifier => _remoteIdentifier;

    public RemotePairingTunnelService(string remoteIdentifier, string hostname, ushort port) : base() {
        _remoteIdentifier = remoteIdentifier;
        Hostname = hostname;
        Port = port;
    }

    public async Task ConnectAsync(
        bool autopair = true,
        CancellationToken cancellationToken = default
    ) {
        _client = new TcpClient();
        using (CancellationTokenSource timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(TIMEOUT))) {
            using (CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token)) {
                try {
                    await _client.ConnectAsync(Hostname, Port, linkedCts.Token);
                    _stream = _client.GetStream();
                }
                catch {
                    await CloseAsync();
                    throw;
                }
            }
        }

        try {
            await AttemptPairVerifyAsync();

            if (!await ValidatePairingAsync()) {
                throw new ConnectionTerminatedException();
            }

            InitClientServerMainEncryptionKeys();
        }
        catch {
            await CloseAsync();
            throw;
        }
    }

    public override void Close() {
        CloseAsync().GetAwaiter().GetResult();
    }

    public async Task CloseAsync() {
        try {
            if (_stream != null) {
                await _stream.DisposeAsync();
            }
        }
        catch {
            // Suppress shutdown exceptions.
        }

        _stream = null;

        _client?.Dispose();
        _client = null;
    }

    public override async Task<XpcDictionary> ReceiveResponseAsync() {
        if (_stream == null) {
            throw new InvalidOperationException("Not connected.");
        }

        byte[] magicBytes = Encoding.UTF8.GetBytes(REPAIRING_PACKET_MAGIC_STRING);

        byte[] magic = new byte[magicBytes.Length];
        await _stream.ReadExactlyAsync(magic);
        if (!magic.AsSpan().SequenceEqual(magicBytes)) {
            throw new InvalidDataException("Invalid pairing packet magic.");
        }

        byte[] lengthBytes = new byte[2];
        await _stream.ReadExactlyAsync(lengthBytes);

        ushort length = BinaryPrimitives.ReadUInt16BigEndian(lengthBytes);
        byte[] bodyBytes = new byte[length];
        await _stream.ReadExactlyAsync(bodyBytes);

        string encodedJson = Encoding.UTF8.GetString(bodyBytes);
        byte[] jsonBytes = Convert.FromBase64String(encodedJson);
        string json = Encoding.UTF8.GetString(jsonBytes);

        // TODO get the XpcDictionary to decode to just a key value dictionary, rather than whatever it is we will end up with at the moment.
        return JsonSerializer.Deserialize(json, InternalJsonSerialisationContext.Default.XpcDictionary) ?? [];
    }

    public override async Task SendRequestAsync(XpcDictionary data) {
        if (_stream == null) {
            throw new InvalidOperationException("Not connected.");
        }

        string json = JsonSerializer.Serialize(data, InternalJsonSerialisationContext.Default.XpcDictionary);
        byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
        string encodedJson = Convert.ToBase64String(jsonBytes);
        byte[] encodedBytes = Encoding.UTF8.GetBytes(encodedJson);

        ushort messageLength = (ushort) encodedBytes.Length;

        byte[] packet = [
            .. Encoding.UTF8.GetBytes(REPAIRING_PACKET_MAGIC_STRING),
            .. EndianBitConverter.BigEndian.GetBytes(messageLength),
            .. encodedBytes
        ];

        await _stream.WriteAsync(packet);
        await _stream.FlushAsync();
    }
}

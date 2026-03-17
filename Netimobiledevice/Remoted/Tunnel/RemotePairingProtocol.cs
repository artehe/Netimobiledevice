using Netimobiledevice.Remoted.Xpc;
using System;
using System.Threading.Tasks;

namespace Netimobiledevice.Remoted.Tunnel;

public abstract class RemotePairingProtocol : StartTcpTunnel
{
    private const int WIRE_PROTOCOL_VERSION = 19;

    private int _encryptedSequenceNumber;
    private ulong _sequenceNumber;
    private string _hostname = string.Empty;

    public string RemoteDeviceModel => HandshakeInfo?["peerDeviceInfo"].AsXpcDictionary()["model"].AsXpcString().Data ?? string.Empty;
    public override string RemoteIdentifier => HandshakeInfo?["peerDeviceInfo"].AsXpcDictionary()["identifier"].AsXpcString().Data ?? string.Empty;

    public XpcDictionary? HandshakeInfo { get; set; }

    public RemotePairingProtocol() : base() {
        _sequenceNumber = 0;
        _encryptedSequenceNumber = 0;
    }

    private async Task AttemptPairVerifyAsync() {
        XpcDictionary handshakeData = new() {
            { "hostOptions", new XpcDictionary() { { "attemptPairVerify", new XpcBool(true) } } },
            { "wireProtocolVersion",new XpcInt64(WIRE_PROTOCOL_VERSION) },
        };
        HandshakeInfo = await SendReceiveHandshakeAsync(handshakeData);
    }

    private void InitClientServerMainEncryptionKeys() {
        // TODO
    }

    private async Task PairAsync() {
        // TODO
    }

    private async Task<XpcDictionary> ReceivePlainResponseAsync() {
        XpcDictionary response = await ReceiveResponseAsync();

        XpcDictionary message = response["message"].AsXpcDictionary();
        XpcDictionary plain = message["plain"].AsXpcDictionary();
        XpcDictionary payload = plain["_0"].AsXpcDictionary();

        return payload;
    }

    private async Task SendPlainRequestAsync(XpcDictionary plainRequest) {
        XpcDictionary request = new() {
            { "message", new XpcDictionary() { { "plain", new XpcDictionary() { { "_0", plainRequest } } } } },
            { "originatedBy", new XpcString("host") },
            { "sequenceNumber", new XpcUInt64(_sequenceNumber) }
        };
        await SendRequestAsync(request);
        _sequenceNumber++;
    }

    private async Task<XpcDictionary> SendReceiveHandshakeAsync(XpcDictionary handshakeData) {
        XpcDictionary request = new() {
            { "request", new XpcDictionary() { { "_0", new XpcDictionary() { { "handshake", new XpcDictionary() { { "_0", handshakeData } } } } } } }
        };
        XpcDictionary response = await SendReceivePlainRequestAsync(request).ConfigureAwait(false);

        XpcDictionary responseDict = response["response"].AsXpcDictionary();
        XpcDictionary inner = responseDict["_1"].AsXpcDictionary();
        XpcDictionary handshake = inner["handshake"].AsXpcDictionary();
        XpcDictionary result = handshake["_0"].AsXpcDictionary();

        return result;
    }


    private async Task<XpcDictionary> SendReceivePlainRequestAsync(XpcDictionary plainRequest) {
        await SendPlainRequestAsync(plainRequest);
        return await ReceivePlainResponseAsync();
    }

    private async Task<bool> ValidatePairingAsync() {
        // TODO
        return true;
    }

    public async Task ConnectAsync(bool autopair = true) {
        await AttemptPairVerifyAsync();

        if (await ValidatePairingAsync()) {
            // Pairing record validation succeeded, so we can just initiate the relevant session keys
            InitClientServerMainEncryptionKeys();
            return;
        }

        if (autopair) {
            await PairAsync();
            Close();

            // Once pairing is completed, the remote endpoint closes the connection,
            // so it must be re-established
            throw new RemotePairingCompletedException();
        }
    }

    public abstract Task<XpcDictionary> ReceiveResponseAsync();

    public abstract Task SendRequestAsync(XpcDictionary data);

    public async Task<XpcDictionary> SendReceiveRequest(XpcDictionary data)
    {
        await SendRequestAsync(data).ConfigureAwait(false);
        return await ReceiveResponseAsync().ConfigureAwait(false);
    }

    public override async Task<TunnelResult> StartTcpTunnelAsync() {
        // TODO
        throw new NotImplementedException();
    }
}

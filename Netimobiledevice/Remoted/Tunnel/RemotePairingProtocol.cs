using Netimobiledevice.Remoted.Xpc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Netimobiledevice.Remoted.Tunnel;

public abstract class RemotePairingProtocol : StartTcpTunnel
{
    private const int WIRE_PROTOCOL_VERSION = 19;

    private int _encryptedSequenceNumber;
    private ulong _sequenceNumber;
    private string _hostname = string.Empty;

    public string RemoteDeviceModel => HandshakeInfo["peerDeviceInfo"]["model"].ToString();
    public override string RemoteIdentifier => HandshakeInfo["peerDeviceInfo"]["identifier"];

    public dynamic? HandshakeInfo { get; set; }

    public RemotePairingProtocol() : base() {
        _sequenceNumber = 0;
        _encryptedSequenceNumber = 0;
    }

    private async Task AttemptPairVerifyAsync() {
        Dictionary<string, object> handshakeData = new Dictionary<string, object> {
            ["hostOptions"] = new Dictionary<string, object> {
                ["attemptPairVerify"] = true
            },
            ["wireProtocolVersion"] = new XpcInt64(WIRE_PROTOCOL_VERSION)
        };
        HandshakeInfo = await SendReceiveHandshakeAsync(handshakeData);
    }

    private async Task<Dictionary<string, object>> ReceivePlainResponseAsync() {
        Dictionary<string, object> response = await ReceiveResponseAsync();

        Dictionary<string, object> message = (Dictionary<string, object>) response["message"];
        Dictionary<string, object> plain = (Dictionary<string, object>) message["plain"];
        Dictionary<string, object> payload = (Dictionary<string, object>) plain["_0"];

        return payload;
    }

    private async Task SendPlainRequestAsync(Dictionary<string, object> plainRequest) {
        Dictionary<string, object> request = new Dictionary<string, object> {
            ["message"] = new Dictionary<string, object> {
                ["plain"] = new Dictionary<string, object> {
                    ["_0"] = plainRequest
                }
            },
            ["originatedBy"] = "host",
            ["sequenceNumber"] = new XpcUInt64(_sequenceNumber)
        };
        await SendRequestAsync(request);
        _sequenceNumber++;
    }

    private async Task<Dictionary<string, object>> SendReceiveHandshakeAsync(Dictionary<string, object> handshakeData) {
        Dictionary<string, object> request = new Dictionary<string, object> {
            ["request"] = new Dictionary<string, object> {
                ["_0"] = new Dictionary<string, object> {
                    ["handshake"] = new Dictionary<string, object> {
                        ["_0"] = handshakeData
                    }
                }
            }
        };

        Dictionary<string, object> response = await SendReceivePlainRequestAsync(request);

        Dictionary<string, object> responseDict = (Dictionary<string, object>) response["response"];
        Dictionary<string, object> inner = (Dictionary<string, object>) responseDict["_1"];
        Dictionary<string, object> handshake = (Dictionary<string, object>) inner["handshake"];
        Dictionary<string, object> result = (Dictionary<string, object>) handshake["_0"];

        return result;
    }


    private async Task<Dictionary<string, object>> SendReceivePlainRequestAsync(Dictionary<string, object> plainRequest) {
        await SendPlainRequestAsync(plainRequest);
        return await ReceivePlainResponseAsync();
    }

    public async Task ConnectAsync(bool autopair = true) {
        await AttemptPairVerifyAsync();

        /* TODO
        if (await ValidatePairingAsync()) {
            // Pairing record validation succeeded, so we can just initiate the relevant session keys
            InitClientServerMainEncryptionKeys();
            return;
        }
        */

        if (autopair) {
            // TODO await PairAsync();
            Close();

            // Once pairing is completed, the remote endpoint closes the connection,
            // so it must be re-established
            throw new RemotePairingCompletedException();
        }
    }

    public abstract Dictionary<string, object> ReceiveResponse();

    public abstract Task<Dictionary<string, object>> ReceiveResponseAsync();

    public abstract void SendRequest(Dictionary<string, object> data);

    public abstract Task SendRequestAsync(Dictionary<string, object> data);

    public async Task<Dictionary<string, object>> SendReceiveRequest(Dictionary<string, object> data)
    {
        await SendRequestAsync(data).ConfigureAwait(false);
        return await ReceiveResponseAsync().ConfigureAwait(false);
    }

    public async Task<TunnelResult> StartTcpTunnelAsync() {
        // TODO
        throw new NotImplementedException();
    }
}

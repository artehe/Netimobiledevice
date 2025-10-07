using System.Collections.Generic;
using System.Threading.Tasks;

namespace Netimobiledevice.Remoted.Tunnel;

public abstract class RemotePairingProtocol : StartTcpTunnel {
    private const int WIRE_PROTOCOL_VERSION = 19;

    private int _sequenceNumber;
    private int _encryptedSequenceNumber;

    public override string RemoteIdentifier => HandshakeInfo["peerDeviceInfo"]["identifier"];

    public dynamic? HandshakeInfo { get; set; }

    public RemotePairingProtocol() : base() { }

    public abstract Task<Dictionary<string, object>> ReceiveResponse();

    public abstract Task SendRequest(Dictionary<string, object> data);

    public async Task<Dictionary<string, object>> SendReceiveRequest(Dictionary<string, object> data) {
        await SendRequest(data);
        return await ReceiveResponse();
    }

    public virtual void Connect(bool autopair = true) {
        // TODO
    }

    public virtual Task ConnectAsync(bool autopair = true) {
        // TODO
        return Task.CompletedTask;
    }

    public string RemoteDeviceModel => HandshakeInfo["peerDeviceInfo"]["model"].ToString();
}

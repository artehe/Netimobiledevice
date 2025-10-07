using System.Collections.Generic;
using System.Threading.Tasks;

namespace Netimobiledevice.Remoted.Tunnel;

public class RemotePairingTunnelService : RemotePairingProtocol {

    private ushort _port;

    public string Hostname { get; private set; }
    public override string RemoteIdentifier { get; }

    public RemotePairingTunnelService(string remoteIdentifier, string hostname, ushort port) : base() {
        RemoteIdentifier = remoteIdentifier;
        Hostname = hostname;
        _port = port;
    }

    public override void Close() {
        /* TODO
        if self._writer is None:
            return
        self._writer.close()
        try:
            await self._writer.wait_closed()
        except ssl.SSLError:
            pass
        self._writer = None
        self._reader = None

         */
    }

    public Task CloseAsync() {
        /* TODO
        if self._writer is None:
            return
        self._writer.close()
        try:
            await self._writer.wait_closed()
        except ssl.SSLError:
            pass
        self._writer = None
        self._reader = None
         */
    }

    public override async Task Connect(bool autopair = true) {
        /* TODO
        fut = asyncio.open_connection(self.hostname, self.port)
        self._reader, self._writer = await asyncio.wait_for(fut, timeout=TIMEOUT)

        try:
            await self._attempt_pair_verify()
            if not await self._validate_pairing():
                raise ConnectionAbortedError()
            self._init_client_server_main_encryption_keys()
        except:  # noqa: E722
            await self.close()
            raise
        */
    }

    public override async Task ConnectAsync(bool autopair = true) {
        /* TODO
        fut = asyncio.open_connection(self.hostname, self.port)
        self._reader, self._writer = await asyncio.wait_for(fut, timeout=TIMEOUT)

        try:
            await self._attempt_pair_verify()
            if not await self._validate_pairing():
                raise ConnectionAbortedError()
            self._init_client_server_main_encryption_keys()
        except:  # noqa: E722
            await self.close()
            raise
        */
    }

    public override Task<Dictionary<string, object>> ReceiveResponse() {
        /* TODO
        await self._reader.readexactly(len(REPAIRING_PACKET_MAGIC))
        size = struct.unpack('>H', await self._reader.readexactly(2))[0]
        return json.loads(await self._reader.readexactly(size))
         */
    }

    public override Task SendRequest(Dictionary<string, object> data) {
        /* TODO
        self._writer.write(RPPairingPacket.build({'body': json.dumps(data, default=self._default_json_encoder).encode()}))
        await self._writer.drain()
         */
    }
}

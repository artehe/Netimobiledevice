namespace Netimobiledevice.Remoted.Tunnel
{
    public class RemotePairingTunnelService : RemotePairingProtocol
    {
        private ushort _port;

        public string Hostname { get; private set; }
        public string RemoteIdentifier { get; private set; }

        public RemotePairingTunnelService(string remoteIdentifier, string hostname, ushort port) : base()
        {
            RemoteIdentifier = remoteIdentifier;
            Hostname = hostname;
            _port = port;
        }
    }

    /* TODO
class RemotePairingTunnelService(RemotePairingProtocol):
    def __init__(self, remote_identifier: str, hostname: str, port: int) -> None:
        self._reader: Optional[StreamReader] = None
        self._writer: Optional[StreamWriter] = None

    async def connect(self, autopair: bool = True) -> None:
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

    async def close(self) -> None:
        if self._writer is None:
            return
        self._writer.close()
        try:
            await self._writer.wait_closed()
        except ssl.SSLError:
            pass
        self._writer = None
        self._reader = None

    async def receive_response(self) -> dict:
        await self._reader.readexactly(len(REPAIRING_PACKET_MAGIC))
        size = struct.unpack('>H', await self._reader.readexactly(2))[0]
        return json.loads(await self._reader.readexactly(size))

    async def send_request(self, data: dict) -> None:
        self._writer.write(
            RPPairingPacket.build({'body': json.dumps(data, default=self._default_json_encoder).encode()}))
        await self._writer.drain()

    @staticmethod
    def _default_json_encoder(obj) -> str:
        if isinstance(obj, bytes):
            return base64.b64encode(obj).decode()
        raise TypeError()

    @staticmethod
    def _decode_bytes_if_needed(data: bytes) -> bytes:
        return base64.b64decode(data)

    def __repr__(self) -> str:
        return (f'<{self.__class__.__name__} IDENTIFIER:{self.remote_identifier} HOSTNAME:{self.hostname} '
                f'PORT:{self.port}>')
    */
}

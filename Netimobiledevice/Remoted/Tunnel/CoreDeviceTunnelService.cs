namespace Netimobiledevice.Remoted.Tunnel
{
    public class CoreDeviceTunnelService : RemotePairingProtocol
    {
        private const string SERVICE_NAME = "com.apple.internal.dt.coredevice.untrusted.tunnelservice";

        private readonly RemoteService _remoteService;
        private int? version;

        public CoreDeviceTunnelService(RemoteServiceDiscoveryService rsd) : base()
        {
            _remoteService = new RemoteService(rsd, SERVICE_NAME);
        }

        public override void Close()
        {
            // TODO  await self.rsd.close()
            // TODO await self.service.close()
        }

        /* TODO
class CoreDeviceTunnelService(RemotePairingProtocol, RemoteService):
    async def connect(self, autopair: bool = True) -> None:
        await RemoteService.connect(self)
        try:
            response = await self.service.receive_response()
            self.version = response['ServiceVersion']
            await RemotePairingProtocol.connect(self, autopair=autopair)
            self.hostname = self.service.address[0]
        except Exception as e:  # noqa: E722
            await self.service.close()
            if isinstance(e, UserDeniedPairingError):
                raise

    async def receive_response(self) -> dict:
        response = await self.service.receive_response()
        return response['value']

    async def send_request(self, data: dict) -> None:
        return await self.service.send_request({
            'mangledTypeName': 'RemotePairing.ControlChannelMessageEnvelope', 'value': data})

         */
    }
}

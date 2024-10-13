using Netimobiledevice.Lockdown;
using Netimobiledevice.Remoted.Xpc;
using System.Threading.Tasks;

namespace Netimobiledevice.Remoted
{
    public class RemoteServiceDiscoveryService : LockdownServiceProvider
    {
        public const ushort RSD_PORT = 58783;

        private readonly RemoteXPCConnection _service;

        public RemoteServiceDiscoveryService(string ip, ushort port) : base()
        {
            _service = new RemoteXPCConnection(ip, port);

            /* TODO
    def __init__(self, address: tuple[str, int], name: Optional[str] = None) -> None:
        self.peer_info: Optional[dict] = None
        self.lockdown: Optional[LockdownClient] = None
        self.all_values: Optional[dict] = None
            */
        }

        public async Task Connect()
        {
            await _service.Connect();
            /* TODO
    self.peer_info = await self.service.receive_response()
    self.udid = self.peer_info['Properties']['UniqueDeviceID']
    self.product_type = self.peer_info['Properties']['ProductType']
    try:
        self.lockdown = create_using_remote(self.start_lockdown_service('com.apple.mobile.lockdown.remote.trusted'))
    except InvalidServiceError:
        self.lockdown = create_using_remote(
            self.start_lockdown_service('com.apple.mobile.lockdown.remote.untrusted'))
    self.all_values = self.lockdown.all_values
*/

        }
    }
}
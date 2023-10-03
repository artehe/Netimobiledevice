using Netimobiledevice.Plist;
using System.Threading.Tasks;

namespace Netimobiledevice.Lockdown.Services
{
    /// <summary>
    /// Provides the service to show process lists, stream formatted and/or filtered syslogs
    /// as well as getting old stored syslog archives in the PAX format.
    /// </summary>
    public sealed class OsTraceService : BaseService
    {
        protected override string ServiceName => "com.apple.os_trace_relay";

        public OsTraceService(LockdownClient client) : base(client) { }

        public async Task<DictionaryNode> GetPidList()
        {
            DictionaryNode request = new DictionaryNode() {
                { "Request", new StringNode("PidList") },
            };
            await Service.SendPlistAsync(request);

            // Ignore the first received unknown byte
            await Service.ReceiveAsync(1);

            DictionaryNode response = (await Service.ReceivePlistAsync())?.AsDictionaryNode() ?? new DictionaryNode();
            return response;
        }
    }
}

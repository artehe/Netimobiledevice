using Netimobiledevice.Plist;
using System;

namespace Netimobiledevice.Lockdown.Services
{
    public sealed class SpringBoardServicesService : BaseService
    {
        protected override string ServiceName => "com.apple.springboardservices";

        public SpringBoardServicesService(LockdownClient lockdownClient) : base(lockdownClient) { }

        public PropertyNode GetIconPNGData(string bundleId)
        {
            DictionaryNode command = new DictionaryNode {
                { "command", new StringNode("getIconPNGData") },
                { "bundleId", new StringNode(bundleId) }
            };

            DictionaryNode response = Service.SendReceivePlist(command)?.AsDictionaryNode() ?? new DictionaryNode();
            if (response.ContainsKey("pngData")) {
                return response["pngData"];
            }
            throw new Exception("Node pngData doesn't exist in response");
        }
    }
}

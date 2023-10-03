using Netimobiledevice.Plist;
using System.Threading.Tasks;

namespace Netimobiledevice.Lockdown.Services
{
    public sealed class InstallationProxyService : BaseService
    {
        protected override string ServiceName => "com.apple.mobile.installation_proxy";

        public InstallationProxyService(LockdownClient client) : base(client) { }

        public async Task<ArrayNode> Browse(DictionaryNode? options = null, ArrayNode? attributes = null)
        {
            options ??= new DictionaryNode();
            if (attributes != null) {
                options.Add("ReturnAttributes", attributes);
            }

            DictionaryNode command = new DictionaryNode() {
                {"Command", new StringNode("Browse") },
                {"ClientOptions", options }
            };
            Service.SendPlist(command);

            ArrayNode result = new ArrayNode();
            while (true) {
                PropertyNode? response = await Service.ReceivePlistAsync();
                if (response == null) {
                    break;
                }

                DictionaryNode responseDict = response.AsDictionaryNode();

                if (responseDict.ContainsKey("CurrentList")) {
                    ArrayNode data = responseDict["CurrentList"].AsArrayNode();
                    foreach (PropertyNode element in data) {
                        result.Add(element);
                    }
                }

                if (responseDict["Status"].AsStringNode().Value == "Complete") {
                    break;
                }
            }
            return result;
        }
    }
}

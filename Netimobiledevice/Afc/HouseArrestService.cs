using Microsoft.Extensions.Logging;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Plist;

namespace Netimobiledevice.Afc
{
    public sealed class HouseArrestService : AfcService
    {
        private const string SERVICE_NAME = "com.apple.mobile.house_arrest";
        private const string RSD_SERVICE_NAME = "com.apple.mobile.house_arrest.shim.remote";
        private const string VEND_CONTAINER = "VendContainer";
        private const string VEND_DOCUMENTS = "VendDocuments";

        private readonly bool documentsOnly;

        public HouseArrestService(LockdownServiceProvider lockdown, string bundleId, bool documentsOnly, ILogger? logger = null) : base(lockdown, GetServiceName(lockdown), logger)
        {
            string cmd = VEND_CONTAINER;
            if (documentsOnly) {
                cmd = VEND_DOCUMENTS;
            }
            this.documentsOnly = documentsOnly;

            try {
                this.SendCommand(bundleId, cmd);
            }
            catch (AfcException ex) {
                logger?.LogError(ex, "Error sending command to house arrest");
                this.Close();
            }
        }

        public HouseArrestService(LockdownServiceProvider lockdown, string bundleId, ILogger? logger = null) : this(lockdown, bundleId, false, logger) { }

        private static string GetServiceName(LockdownServiceProvider lockdown)
        {
            if (lockdown is LockdownClient) {
                return SERVICE_NAME;
            }
            return RSD_SERVICE_NAME;
        }

        public void SendCommand(string bundleId, string cmd = VEND_CONTAINER)
        {
            DictionaryNode request = new DictionaryNode() {
                { "Command", new StringNode(cmd) },
                { "Identifier", new StringNode(bundleId) }
            };
            PropertyNode? response = this.Service.SendReceivePlist(request);
            DictionaryNode responseDict = response?.AsDictionaryNode() ?? new DictionaryNode();
            if (responseDict.TryGetValue("Error", out PropertyNode? value)) {
                throw new AfcException(value.AsStringNode().Value);
            }
        }
    }
}

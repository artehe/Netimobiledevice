using Microsoft.Extensions.Logging;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Plist;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Afc;

public sealed class HouseArrestService : AfcService
{
    private const string SERVICE_NAME = "com.apple.mobile.house_arrest";
    private const string RSD_SERVICE_NAME = "com.apple.mobile.house_arrest.shim.remote";
    private const string VEND_CONTAINER = "VendContainer";
    private const string VEND_DOCUMENTS = "VendDocuments";

    private HouseArrestService(LockdownServiceProvider lockdown, string serviceName, ILogger? logger = null) : base(lockdown, serviceName, logger) { }

    public static HouseArrestService Create(LockdownServiceProvider lockdown, string bundleId, bool documentsOnly = false, ILogger? logger = null)
    {
        string serviceToUse = RSD_SERVICE_NAME;
        if (lockdown is LockdownClient) {
            serviceToUse = SERVICE_NAME;
        }

        HouseArrestService houseArrestService = new HouseArrestService(lockdown, serviceToUse, logger);

        string cmd = VEND_CONTAINER;
        if (documentsOnly) {
            cmd = VEND_DOCUMENTS;
        }

        try {
            houseArrestService.SendCommand(bundleId, cmd);
        }
        catch (AfcException ex) {
            logger?.LogError(ex, "Error sending command to house arrest");
            houseArrestService.Close();
            throw;
        }

        return houseArrestService;
    }

    public static async Task<HouseArrestService> CreateAsync(LockdownServiceProvider lockdown, string bundleId, bool documentsOnly = false, ILogger? logger = null, CancellationToken cancellationToken = default)
    {
        string serviceToUse = RSD_SERVICE_NAME;
        if (lockdown is LockdownClient) {
            serviceToUse = SERVICE_NAME;
        }

        HouseArrestService houseArrestService = new HouseArrestService(lockdown, serviceToUse, logger);

        string cmd = VEND_CONTAINER;
        if (documentsOnly) {
            cmd = VEND_DOCUMENTS;
        }

        try {
            await houseArrestService.SendCommandAsync(bundleId, cmd, cancellationToken).ConfigureAwait(false);
        }
        catch (AfcException ex) {
            logger?.LogError(ex, "Error sending command to house arrest");
            houseArrestService.Close();
            throw;
        }

        return houseArrestService;
    }

    public DictionaryNode SendCommand(string bundleId, string cmd = VEND_CONTAINER)
    {
        DictionaryNode request = new DictionaryNode() {
            { "Command", new StringNode(cmd) },
            { "Identifier", new StringNode(bundleId) }
        };

        PropertyNode? response = this.Service.SendReceivePlist(request);

        DictionaryNode responseDict = response?.AsDictionaryNode() ?? [];
        if (responseDict.TryGetValue("Error", out PropertyNode? value)) {
            string err = value.AsStringNode().Value;
            if (err == "ApplicationLookupFailed") {
                throw new AfcException($"No app with bundle id {bundleId} found");
            }
            else {
                throw new NetimobiledeviceException(err);
            }
        }
        return responseDict;
    }

    public async Task<DictionaryNode> SendCommandAsync(string bundleId, string cmd = VEND_CONTAINER, CancellationToken cancellationToken = default)
    {
        DictionaryNode request = new DictionaryNode() {
            { "Command", new StringNode(cmd) },
            { "Identifier", new StringNode(bundleId) }
        };

        PropertyNode? response = await this.Service.SendReceivePlistAsync(request, cancellationToken).ConfigureAwait(false);

        DictionaryNode responseDict = response?.AsDictionaryNode() ?? [];
        if (responseDict.TryGetValue("Error", out PropertyNode? value)) {
            string err = value.AsStringNode().Value;
            if (err == "ApplicationLookupFailed") {
                throw new AfcException($"No app with bundle id {bundleId} found");
            }
            else {
                throw new NetimobiledeviceException(err);
            }
        }
        return responseDict;
    }
}

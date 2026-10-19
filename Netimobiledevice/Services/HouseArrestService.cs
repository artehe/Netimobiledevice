using Microsoft.Extensions.Logging;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Plist;
using Netimobiledevice.Services.HouseArrest;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Services;

/// <summary>
/// AFC access to an installed application's container.
/// </summary>
public sealed class HouseArrestService(
    LockdownServiceProvider lockdown,
    bool documentsOnly = false,
    ILogger? logger = null
) : AfcService(
    lockdown,
    lockdown is LockdownClient ? LockdownServiceName : RemoteServiceName,
    logger: logger
    ) {
    private const string VendContainer = "VendContainer";
    private const string VendDocuments = "VendDocuments";

    public new const string LockdownServiceName = "com.apple.mobile.house_arrest";
    public new const string RemoteServiceName = "com.apple.mobile.house_arrest.shim.remote";

    /// <summary>Whether only the container's <c>Documents</c> subtree is vended.</summary>
    public bool DocumentsOnly { get; } = documentsOnly;

    /// <summary>
    /// Create a service already vending the container of the given application.
    /// </summary>
    /// <remarks>
    /// Instantiates the service and immediately issues the vend command, leaving the AFC
    /// session rooted at the application's container. On failure the service is closed
    /// before the error propagates.
    /// </remarks>
    /// <param name="lockdown">Service provider used to start the service and reach the device.</param>
    /// <param name="bundleId">Bundle identifier of the application whose container to vend.</param>
    /// <param name="documentsOnly">
    /// When <c>true</c>, vend only the container's <c>Documents</c> subtree (<c>VendDocuments</c>);
    /// otherwise vend the whole container (<c>VendContainer</c>).
    /// </param>
    /// <param name="cancellationToken">Token to cancel the vend request.</param>
    /// <returns>A connected <see cref="HouseArrestService"/> rooted at the application's container.</returns>
    /// <exception cref="AppNotInstalledException">No application with <paramref name="bundleId"/> is installed.</exception>
    /// <exception cref="HouseArrestException">The device reported any other vend error.</exception>
    public static async Task<HouseArrestService> CreateAsync(
        LockdownServiceProvider lockdown,
        string bundleId,
        bool documentsOnly = false,
        CancellationToken cancellationToken = default
    ) {
        HouseArrestService service = new HouseArrestService(lockdown, documentsOnly);
        string cmd = documentsOnly ? VendDocuments : VendContainer;

        await service.ConnectAsync(cancellationToken);
        try {
            await service.SendCommandAsync(bundleId, cmd, cancellationToken).ConfigureAwait(false);
        }
        catch {
            await service.CloseAsync().ConfigureAwait(false);
            throw;
        }
        return service;
    }


    /// <summary>
    /// Vend an application's container so subsequent AFC operations act on it.
    /// </summary>
    /// <param name="bundleId">Bundle identifier of the application to vend.</param>
    /// <param name="cmd">
    /// Vend command, either <c>VendContainer</c> (whole container) or
    /// <c>VendDocuments</c> (only the <c>Documents</c> subtree).
    /// </param>
    /// <param name="cancellationToken">Token to cancel the request.</param>
    /// <exception cref="AppNotInstalledException">No application with <paramref name="bundleId"/> is installed.</exception>
    /// <exception cref="HouseArrestException">The device reported any other error.</exception>
    public async Task SendCommandAsync(
        string bundleId,
        string cmd = VendContainer,
        CancellationToken cancellationToken = default
    ) {
        DictionaryNode request = new DictionaryNode() {
            { "Command", new StringNode(cmd) },
            { "Identifier", new StringNode(bundleId) }
        };

        if (Service is null) {
            throw new HouseArrestException("Service is null");
        }
        PropertyNode? response = await Service.SendReceivePlistAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);

        DictionaryNode responseDict = response?.AsDictionaryNode() ?? [];
        if (responseDict.TryGetValue("Error", out PropertyNode? value)) {
            string error = value.AsStringNode().Value;
            throw error == "ApplicationLookupFailed"
                ? new AppNotInstalledException($"No app with bundle id {bundleId} found")
                : new HouseArrestException(error);
        }
    }
}

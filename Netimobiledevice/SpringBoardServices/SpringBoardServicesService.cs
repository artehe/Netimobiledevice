using Microsoft.Extensions.Logging;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Plist;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.SpringBoardServices;

/// <summary>
/// Provides a service to interact with the home screen getting icons from the installed apps on the device, the current device wallpaper, or the orientation of the device.
/// </summary>
/// <param name="lockdown"></param>
/// <param name="logger"></param>
public sealed class SpringBoardServicesService(LockdownServiceProvider lockdown, ILogger? logger = null) : LockdownService(lockdown, LOCKDOWN_SERVICE_NAME, RSD_SERVICE_NAME, logger: logger)
{
    private const string LOCKDOWN_SERVICE_NAME = "com.apple.springboardservices";
    private const string RSD_SERVICE_NAME = "com.apple.springboardservices.shim.remote";

    private static DictionaryNode CreateCommand(string command)
    {
        DictionaryNode cmd = new DictionaryNode() {
            { "command", new StringNode(command) },
        };
        return cmd;
    }

    private PropertyNode ExecuteCommand(DictionaryNode command, string responseNode)
    {
        DictionaryNode response = Service.SendReceivePlist(command)?.AsDictionaryNode() ?? [];
        if (response.TryGetValue(responseNode, out PropertyNode? node)) {
            return node;
        }
        throw new SpringBoardServicessException($"Key {responseNode} not found doesn't exist in response");
    }

    private async Task<PropertyNode> ExecuteCommandAsync(DictionaryNode command, string responseNode, CancellationToken cancellationToken)
    {
        PropertyNode? response = await Service.SendReceivePlistAsync(command, cancellationToken).ConfigureAwait(false);
        DictionaryNode dict = response?.AsDictionaryNode() ?? [];
        if (dict.TryGetValue(responseNode, out PropertyNode? node)) {
            return node;
        }
        throw new SpringBoardServicessException($"Key {responseNode} not found doesn't exist in response");
    }

    /// <summary>
    /// Get the icon of the application with the specified <paramref name="bundleId"/>.
    /// </summary>
    /// <param name="bundleId">The bundle identifier of the applicaition.</param>
    /// <returns>The byte array containing the PNG icon.</returns>
    public DataNode GetIconPngData(string bundleId)
    {
        DictionaryNode command = CreateCommand("getIconPNGData");
        command.Add("bundleId", new StringNode(bundleId));
        return ExecuteCommand(command, "pngData").AsDataNode();
    }

    /// <summary>
    /// Get the icon of the application with the specified <paramref name="bundleId"/>.
    /// </summary>
    /// <param name="bundleId">The bundle identifier of the applicaition.</param>
    /// <returns>The byte array containing the PNG icon.</returns>
    public async Task<DataNode> GetIconPngDataASync(string bundleId, CancellationToken cancellationToken = default)
    {
        DictionaryNode command = CreateCommand("getIconPNGData");
        command.Add("bundleId", new StringNode(bundleId));
        PropertyNode result = await ExecuteCommandAsync(command, "pngData", cancellationToken).ConfigureAwait(false);
        return result.AsDataNode();
    }

    /// <summary>
    /// Get the orientation of the device's screen.
    /// </summary>
    public ScreenOrientation GetScreenOrientation()
    {
        DictionaryNode command = CreateCommand("getInterfaceOrientation");
        return (ScreenOrientation) ExecuteCommand(command, "interfaceOrientation").AsIntegerNode().Value;
    }

    /// <summary>
    /// Get the orientation of the device's screen.
    /// </summary>
    public async Task<ScreenOrientation> GetScreenOrientationAsync(CancellationToken cancellationToken = default)
    {
        DictionaryNode command = CreateCommand("getInterfaceOrientation");
        PropertyNode result = await ExecuteCommandAsync(command, "interfaceOrientation", cancellationToken).ConfigureAwait(false);
        return (ScreenOrientation) result.AsIntegerNode().Value;
    }

    /// <summary>
    /// Get the wallpaper image of the device.
    /// </summary>
    /// <returns>The byte array containing the PNG wallpaper.</returns>
    public DataNode GetWallpaperPngData()
    {
        DictionaryNode command = CreateCommand("getHomeScreenWallpaperPNGData");
        return ExecuteCommand(command, "pngData").AsDataNode();
    }

    /// <summary>
    /// Get the wallpaper image of the device.
    /// </summary>
    /// <returns>The byte array containing the PNG wallpaper.</returns>
    public async Task<DataNode> GetWallpaperPngDataAsync(CancellationToken cancellationToken = default)
    {
        DictionaryNode command = CreateCommand("getHomeScreenWallpaperPNGData");
        PropertyNode result = await ExecuteCommandAsync(command, "pngData", cancellationToken).ConfigureAwait(false);
        return result.AsDataNode();
    }

    /// <summary>
    /// Sets the icon state of the connected device.
    /// </summary>
    /// <param name="newState">A plist containing the new iconstate.</param>
    public DictionaryNode SetIconState(DictionaryNode? newState = null)
    {
        DictionaryNode command = CreateCommand("setIconState");
        newState ??= [];
        command.Add("iconState", newState);
        return Service.SendReceivePlist(command)?.AsDictionaryNode() ?? [];
    }

    /// <summary>
    /// Sets the icon state of the connected device.
    /// </summary>
    /// <param name="newState">A plist containing the new iconstate.</param>
    public async Task<DictionaryNode> SetIconStateAsync(DictionaryNode? newState = null, CancellationToken cancellationToken = default)
    {
        DictionaryNode command = CreateCommand("setIconState");
        newState ??= [];
        command.Add("iconState", newState);

        PropertyNode? result = await Service.SendReceivePlistAsync(command, cancellationToken).ConfigureAwait(false);
        return result?.AsDictionaryNode() ?? [];
    }
}

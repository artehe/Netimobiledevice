using Microsoft.Extensions.Logging;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Plist;
using Netimobiledevice.Services.Springboard;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Services;

/// <summary>
/// Provides a service to interact with the home screen getting icons from the installed apps on the device,
/// the current device wallpaper, or the orientation of the device.
/// </summary>
/// <param name="lockdown"></param>
/// <param name="logger"></param>
public sealed class SpringBoardServicesService(
    LockdownServiceProvider lockdown,
    ILogger? logger = null
) : LockdownService(
    lockdown,
    lockdown is LockdownClient ? LockdownServiceName : RemoteServiceName,
    logger: logger
) {
    private const string LockdownServiceName = "com.apple.springboardservices";
    private const string RemoteServiceName = "com.apple.springboardservices.shim.remote";

    private static DictionaryNode CreateCommand(string command) => new DictionaryNode() {
        { "command", new StringNode(command) },
    };

    private async Task<PropertyNode> ExecuteCommandAsync(
        DictionaryNode command,
        string responseNode,
        CancellationToken cancellationToken
    ) {
        if (Service is null) {
            throw new SpringBoardServicessException("Service is null");
        }

        PropertyNode? response = await Service.SendReceivePlistAsync(command, cancellationToken);
        DictionaryNode responseDict = response?.AsDictionaryNode() ?? [];
        if (responseDict.TryGetValue(responseNode, out PropertyNode? node)) {
            return node;
        }
        throw new SpringBoardServicessException($"Key {responseNode} not found doesn't exist in response");
    }

    /// <summary>Retrieve the home screen icon layout metrics.</summary>
    /// <returns>Mapping of metric names to their numeric values, as reported by SpringBoard.</returns>
    public async Task<DictionaryNode> GetHomeScreenIconMetricsAsync(CancellationToken cancellationToken = default) {
        DictionaryNode command = CreateCommand("getHomeScreenIconMetrics");

        if (Service is null) {
            throw new SpringBoardServicessException("Service is null");
        }

        PropertyNode? response = await Service.SendReceivePlistAsync(command, cancellationToken);
        DictionaryNode responseDict = response?.AsDictionaryNode() ?? [];

        return responseDict;
    }

    /// <summary>
    /// Retrieve the home screen icon image of an installed application.
    /// </summary>
    /// <param name="bundleId">Bundle identifier of the application whose icon is requested.</param>
    /// <returns>PNG-encoded icon image bytes, or null if no <c>pngData</c> is returned.</returns>
    public async Task<DataNode> GetIconPngDataAsync(
        string bundleId,
        CancellationToken cancellationToken = default
    ) {
        DictionaryNode command = CreateCommand("getIconPNGData");
        command.Add("bundleId", new StringNode(bundleId));

        PropertyNode result = await ExecuteCommandAsync(command, "pngData", cancellationToken).ConfigureAwait(false);
        return result.AsDataNode();
    }

    /// <summary>
    /// Retrieve the current home screen icon layout.
    /// </summary>
    /// <param name="formatVersion">
    /// Icon state format version sent to SpringBoard as <c>formatVersion</c>.
    /// When null or empty, the key is omitted from the request.
    /// </param>
    /// <returns>Nested array describing the home screen pages, folders and icons.</returns>
    public async Task<ArrayNode> GetIconStateAsync(
        string? formatVersion = "2",
        CancellationToken cancellationToken = default
    ) {
        DictionaryNode cmd = CreateCommand("getIconState");
        if (!string.IsNullOrEmpty(formatVersion)) {
            cmd.Add("formatVersion", new StringNode(formatVersion));
        }

        if (Service is null) {
            throw new SpringBoardServicessException("Service is null");
        }

        PropertyNode? responseNode = await Service.SendReceivePlistAsync(cmd, cancellationToken).ConfigureAwait(false);
        return responseNode?.AsArrayNode() ?? [];
    }

    /// <summary>
    /// Query the current SpringBoard interface orientation.
    /// </summary>
    public async Task<ScreenOrientation> GetInterfaceOrientationAsync(CancellationToken cancellationToken = default) {
        DictionaryNode command = CreateCommand("getInterfaceOrientation");
        PropertyNode response = await ExecuteCommandAsync(command, "interfaceOrientation", cancellationToken);
        long value = response.AsIntegerNode().SignedValue;
        if (!Enum.IsDefined(typeof(ScreenOrientation), value)) {
            throw new SpringBoardServicessException($"{value} is not a valid {nameof(ScreenOrientation)}");
        }
        return (ScreenOrientation) value;
    }

    /// <summary>Retrieve metadata about a named wallpaper.</summary>
    /// <param name="wallpaperName">Name of the wallpaper to query.</param>
    /// <returns>Mapping describing the wallpaper, as reported by SpringBoard.</returns>
    public async Task<DictionaryNode> GetWallpaperInfoAsync(string wallpaperName, CancellationToken cancellationToken = default) {
        DictionaryNode command = CreateCommand("getWallpaperInfo");
        command.Add("wallpaperName", new StringNode(wallpaperName));

        if (Service is null) {
            throw new SpringBoardServicessException("Service is null");
        }

        PropertyNode? response = await Service.SendReceivePlistAsync(command, cancellationToken);
        return response?.AsDictionaryNode() ?? [];
    }

    /// <summary>
    /// Retrieve the current home screen wallpaper image.
    /// </summary>
    /// <returns>
    /// PNG-encoded wallpaper image bytes, or null if no <c>pngData</c> is returned.
    /// </returns>
    public async Task<DataNode> GetWallpaperPngDataAsync(CancellationToken cancellationToken = default) {
        DictionaryNode command = CreateCommand("getHomeScreenWallpaperPNGData");
        PropertyNode result = await ExecuteCommandAsync(command, "pngData", cancellationToken).ConfigureAwait(false);
        return result.AsDataNode();
    }

    /// <summary>Retrieve the preview image for a named wallpaper.</summary>
    /// <param name="wallpaperName">Name of the wallpaper whose preview image is requested.</param>
    /// <returns>PNG-encoded preview image bytes.</returns>
    /// <exception cref="KeyNotFoundException">The response contains no <c>pngData</c>.</exception>
    public async Task<DataNode> GetWallpaperPreviewImageAsync(string wallpaperName, CancellationToken cancellationToken = default) {
        DictionaryNode command = CreateCommand("getWallpaperPreviewImage");
        command.Add("wallpaperName", new StringNode(wallpaperName));

        if (Service is null) {
            throw new SpringBoardServicessException("Service is null");
        }

        PropertyNode response = await ExecuteCommandAsync(command, "pngData", cancellationToken);
        return response.AsDataNode();
    }

    /// <summary>
    /// Re-apply the current icon layout by reading it back and writing it unchanged,
    /// forcing SpringBoard to reload its layout.
    /// </summary>
    public async Task ReloadIconStateAsync(CancellationToken cancellationToken = default) {
        ArrayNode state = await GetIconStateAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await SetIconStateAsync(state, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Apply a new home screen icon layout.
    /// </summary>
    /// <param name="newState">
    /// Icon layout in the same structure returned by <see cref="GetIconStateAsync"/>.
    /// When null, an empty layout is sent.
    /// </param>
    public async Task SetIconStateAsync(
        ArrayNode? newState = null,
        CancellationToken cancellationToken = default
    ) {
        newState ??= [];

        DictionaryNode command = CreateCommand("setIconState");
        command.Add("iconState", newState);

        if (Service is null) {
            throw new SpringBoardServicessException("Service is null");
        }
        await Service.SendReceivePlistAsync(command, cancellationToken).ConfigureAwait(false);
    }
}

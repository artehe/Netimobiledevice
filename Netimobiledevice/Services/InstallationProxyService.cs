using Microsoft.Extensions.Logging;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Plist;
using Netimobiledevice.Services.InstallationProxy;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Services;

/// <summary>
/// Client for the installation_proxy service.
/// <para>
/// Provides access to the device's application installation database: installing, upgrading,
/// uninstalling, archiving/restoring, browsing and looking up apps, as well as uploading carrier
/// bundles (IPCC). Most operations send a plist command over the service connection and stream back
/// progress and status responses.
/// </para>
/// </summary>
public sealed class InstallationProxyService(
    LockdownServiceProvider lockdown,
    ILogger? logger = null
) : LockdownService(
    lockdown,
    lockdown is LockdownClient ? LockdownServiceName : RemoteServiceName,
    logger: logger
) {
    private const string LockdownServiceName = "com.apple.mobile.installation_proxy";
    private const string RemoteServiceName = "com.apple.mobile.installation_proxy.shim.remote";
    private const string TemporaryRemoteBaseDirectory = "/PublicStaging/Netimobiledevice";

    /// <summary>
    /// Zips an unpackaged <c>.app</c> directory into IPA bytes, placing its contents under
    /// <c>Payload/&lt;directory name&gt;/</c>.
    /// </summary>
    private static async Task<byte[]> CreateIpaContentsFromDirectory(string directory, CancellationToken cancellationToken) {
        string dirName = Path.GetFileName(Path.TrimEndingDirectorySeparator(directory));
        string payloadPrefix = "Payload/" + dirName;

        using (MemoryStream buffer = new MemoryStream()) {
            using (ZipArchive zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true)) {
                foreach (string fullPath in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)) {
                    // Mirror Python's Path.touch(): bump the mtime so every entry gets a valid, current timestamp.
                    File.SetLastWriteTimeUtc(fullPath, DateTime.UtcNow);

                    string relative = Path.GetRelativePath(directory, fullPath).Replace('\\', '/');
                    await zip.CreateEntryFromFileAsync(fullPath, $"{payloadPrefix}/{relative}", CompressionLevel.Optimal, cancellationToken);
                }
            }

            return buffer.ToArray();
        }
    }

    private static string GetApplicationTypeString(ApplicationType applicationType) => applicationType switch {
        ApplicationType.Any => "Any",
        ApplicationType.System => "System",
        ApplicationType.User => "User",
        _ => throw new NotImplementedException($"Unknown application type: {applicationType}")
    };

    /// <summary>
    /// Send a command that targets an installed app by its bundle identifier and wait for completion.
    /// Sends <c>{"Command": cmd, "ApplicationIdentifier": bundleIdentifier, "ClientOptions": options}</c>,
    /// then consumes progress/status responses until the operation completes.
    /// </summary>
    /// <exception cref="AppInstallException">
    /// The service reports an error or finishes without a <c>Complete</c> status.
    /// </exception>
    private async Task SendCmdForBundleIdentifierAsync(
        string bundleIdentifier,
        string cmd = "Archive",
        DictionaryNode? options = null,
        Action<int>? handler = null,
        CancellationToken ct = default
    ) {
        DictionaryNode request = new DictionaryNode {
            ["Command"] = new StringNode(cmd),
            ["ApplicationIdentifier"] = new StringNode(bundleIdentifier),
            ["ClientOptions"] = options ?? [],
        };

        if (Service is null) {
            throw new InstallationProxyException("Service is null");
        }

        await Service.SendPlistAsync(request, cancellationToken: ct);
        await WatchCompletionAsync(handler, ct);
    }

    /// <summary>
    /// Send an install/upgrade command for a package already staged on the device, and wait for completion.
    /// Sends <c>{"Command": cmd, "ClientOptions": options, "PackagePath": packagePath}</c>.
    /// </summary>
    /// <exception cref="AppInstallException">
    /// The service reports an error or finishes without a <c>Complete</c> status.
    /// </exception>
    private async Task SendPackageAsync(
        string cmd,
        DictionaryNode? options,
        Action<int>? handler,
        string packagePath,
        CancellationToken ct = default
    ) {
        DictionaryNode request = new DictionaryNode {
            ["Command"] = new StringNode(cmd),
            ["ClientOptions"] = options ?? [],
            ["PackagePath"] = new StringNode(packagePath),
        };

        if (Service is null) {
            throw new InstallationProxyException("Service is null");
        }

        await Service.SendPlistAsync(request, cancellationToken: ct);
        await WatchCompletionAsync(handler, ct);
    }

    private async Task UploadIpccAsync(Stream fileStream, AfcService afcClient, string dst, CancellationToken ct) {
        if (Logger.IsEnabled(LogLevel.Information)) {
            Logger.LogInformation("Uploading {Dst} contents..", dst);
        }
        await afcClient.MakeDirsAsync(dst, ct);

        // We unpack it and upload it directly instead of saving it in a temp folder
        using (ZipArchive zip = new ZipArchive(fileStream, ZipArchiveMode.Read)) {
            foreach (ZipArchiveEntry entry in zip.Entries) {
                string fileName = entry.FullName;
                if (fileName.EndsWith('/') || fileName.EndsWith('\\')) {
                    await afcClient.MakeDirsAsync($"{dst}/{fileName}", ct);
                    continue;
                }

                using (Stream entryStream = await entry.OpenAsync(ct)) {
                    using (MemoryStream data = new MemoryStream()) {
                        await entryStream.CopyToAsync(data, ct);

                        await afcClient.MakeDirsAsync(dst, ct);
                        await afcClient.SetFileContentsAsync($"{dst}/{fileName}", data.ToArray(), ct);
                    }
                }
            }
        }
        Logger.LogInformation("Upload complete.");
    }

    /// <summary>
    /// Upload a local IPCC (carrier bundle) zip to the device, unpacking it into <paramref name="remotePath"/>.
    /// </summary>
    private async Task UploadIpccFromPathAsync(
        string file,
        string remotePath,
        AfcService afcClient,
        CancellationToken ct = default
    ) {
        byte[] bytes = await File.ReadAllBytesAsync(file, ct);
        if (Logger.IsEnabled(LogLevel.Information)) {
            Logger.LogInformation("Uploading {FileName} contents..", Path.GetFileName(file));
        }
        await UploadIpccAsync(new MemoryStream(bytes, writable: false), afcClient, remotePath, ct);
    }

    private async Task WatchCompletionAsync(Action<int>? handler, CancellationToken ct) {
        while (true) {
            if (Service is null) {
                throw new InstallationProxyException("Service is null");
            }

            PropertyNode? response = await Service.ReceivePlistAsync(ct);
            if (response is null) {
                break;
            }

            DictionaryNode responseDict = response.AsDictionaryNode();
            if (responseDict.Count == 0) {
                break;
            }

            if (responseDict.TryGetValue("Error", out PropertyNode? error)) {
                responseDict.TryGetValue("ErrorDescription", out PropertyNode? description);
                throw new AppInstallException($"{error.AsStringNode().Value}: {description?.AsStringNode().Value}");
            }

            if (responseDict.TryGetValue("PercentComplete", out PropertyNode? completionObj)) {
                int completion = (int) completionObj.AsIntegerNode().SignedValue;
                if (handler is not null) {
                    Logger.LogDebug("calling handler");
                    handler(completion);
                }

                if (Logger.IsEnabled(LogLevel.Information)) {
                    Logger.LogInformation("{Completion}% Complete", completion);
                }
            }

            if (responseDict.TryGetValue("Status", out PropertyNode? status) && status.AsStringNode().Value == "Complete") {
                Logger.LogInformation("Installation succeed.");
                return;
            }
        }

        throw new AppInstallException();
    }

    /// <summary>
    /// Enumerate installed apps via the <c>"Browse"</c> command, accumulating each response's
    /// <c>CurrentList</c> entries until the service reports <c>Complete</c> (or returns an empty response).
    /// </summary>
    public async Task<ArrayNode> BrowseAsync(
        DictionaryNode? options = null,
        ArrayNode? attributes = null,
        CancellationToken ct = default
    ) {
        options ??= [];
        if (attributes is { Count: > 0 }) {
            options["ReturnAttributes"] = attributes;
        }

        if (Service is null) {
            throw new InstallationProxyException("Service is null");
        }

        DictionaryNode cmd = new DictionaryNode {
            ["Command"] = new StringNode("Browse"),
            ["ClientOptions"] = options
        };
        await Service.SendPlistAsync(cmd, cancellationToken: ct);

        ArrayNode result = [];
        while (true) {
            PropertyNode? responseNode = await Service.ReceivePlistAsync(ct);
            if (responseNode is null) {
                break;
            }

            DictionaryNode response = responseNode.AsDictionaryNode();
            if (response.Count == 0) {
                break;
            }


            if (response.TryGetValue("CurrentList", out PropertyNode? data) && data is ArrayNode list) {
                foreach (PropertyNode item in list) {
                    result.Add(item);
                }
            }

            if (response.TryGetValue("Status", out PropertyNode? status) &&
                status is StringNode statusStr &&
                statusStr.Value == "Complete"
            ) {
                break;
            }
        }

        return result;
    }

    /// <summary>
    /// Ask the device whether it satisfies a set of app capabilities (<c>"CheckCapabilitiesMatch"</c>).
    /// <paramref name="capabilities"/> is sent under <c>Capabilities</c> only when non-empty.
    /// </summary>
    /// <returns>The <c>LookupResult</c> value from the response, or <c>null</c> if absent.</returns>
    public async Task<DictionaryNode?> CheckCapabilitiesMatchAsync(
        DictionaryNode? capabilities = null,
        DictionaryNode? options = null,
        CancellationToken ct = default
    ) {
        DictionaryNode cmd = new DictionaryNode {
            ["Command"] = new StringNode("CheckCapabilitiesMatch"),
            ["ClientOptions"] = options ?? [],
        };

        if (capabilities is not null && capabilities.Count > 0) {
            cmd["Capabilities"] = capabilities;
        }


        if (Service is null) {
            throw new InstallationProxyException("Service is null");
        }

        PropertyNode? result = await Service.SendReceivePlistAsync(cmd, cancellationToken: ct).ConfigureAwait(false);
        DictionaryNode dict = result?.AsDictionaryNode() ?? [];

        return dict.TryGetValue("LookupResult", out PropertyNode? value) && value is not null ? value.AsDictionaryNode() : null;
    }

    /// <summary>
    /// Retrieve installed apps, keyed by bundle identifier. When <paramref name="calculateSizes"/> is set,
    /// a second lookup fetches <c>StaticDiskUsage</c>/<c>DynamicDiskUsage</c> and merges them into each entry.
    /// </summary>
    /// <param name="applicationType"><c>"Any"</c> (default), <c>"System"</c> or <c>"User"</c>.</param>
    /// <param name="showPlaceholders">Include placeholder (installing/downloading) apps.</param>
    public async Task<DictionaryNode> GetAppsAsync(
        ApplicationType applicationType = ApplicationType.Any,
        bool calculateSizes = false,
        IList<string>? bundleIdentifiers = null,
        bool showPlaceholders = false,
        CancellationToken ct = default
    ) {
        DictionaryNode options = [];
        if (bundleIdentifiers is not null) {
            ArrayNode bundleIdNode = [];
            foreach (string bundleIdentifier in bundleIdentifiers) {
                bundleIdNode.Add(new StringNode(bundleIdentifier));
            }
            options["BundleIDs"] = bundleIdNode;
        }

        options["ApplicationType"] = new StringNode(GetApplicationTypeString(applicationType));
        if (showPlaceholders) {
            options["ShowPlaceholders"] = new BooleanNode(true);
        }

        DictionaryNode result = await LookupAsync(options, ct) ?? throw new AppInstallException("Lookup response is missing LookupResult");
        if (calculateSizes) {
            options["ReturnAttributes"] = new ArrayNode {
                new StringNode("CFBundleIdentifier"),
                new StringNode("StaticDiskUsage"),
                new StringNode("DynamicDiskUsage")
            };

            DictionaryNode additionalInfo = await LookupAsync(options, ct) ?? throw new AppInstallException("Lookup response is missing LookupResult");
            foreach ((string? bundleIdentifier, PropertyNode? app) in additionalInfo) {
                DictionaryNode target = result[bundleIdentifier].AsDictionaryNode();
                foreach ((string? key, PropertyNode? value) in app.AsDictionaryNode()) {
                    target[key] = value;
                }
                result[bundleIdentifier] = target;
            }
        }

        return result;
    }

    /// <summary>
    /// Install an app or carrier bundle from a local path.
    /// A <c>.ipcc</c> suffix is treated as a carrier bundle (<c>PackageType</c> forced to
    /// <c>"CarrierBundle"</c>); a directory is treated as an unpackaged app and zipped into an
    /// <c>.ipa</c>; any other path is read as <c>.ipa</c> bytes. When <paramref name="developer"/> is
    /// set, <c>PackageType</c> is forced to <c>"Developer"</c>.
    /// </summary>
    /// <exception cref="AppInstallException">The installation fails.</exception>
    public async Task InstallFromLocalAsync(
        string packagePath,
        string cmd = "Install",
        DictionaryNode? options = null,
        Action<int>? handler = null,
        bool developer = false,
        CancellationToken ct = default
    ) {
        options ??= [];

        bool ipccMode = Path.GetExtension(packagePath) == ".ipcc";

        byte[] ipaContents = [];
        if (ipccMode) {
            options["PackageType"] = new StringNode("CarrierBundle");
        }
        else if (Directory.Exists(packagePath)) {
            // treat as app, convert into an ipa
            ipaContents = await CreateIpaContentsFromDirectory(packagePath, ct);
        }
        else {
            // treat as ipa
            ipaContents = await File.ReadAllBytesAsync(packagePath, ct);
        }

        if (developer) {
            options["PackageType"] = new StringNode("Developer");
        }

        await using (AfcService afc = new AfcService(Lockdown)) {
            await afc.ConnectAsync(ct);

            string fname = $"{TemporaryRemoteBaseDirectory}/{Guid.NewGuid()}.{(ipccMode ? "ipcc" : "ipa")}";
            try {
                if (!ipccMode) {
                    await afc.MakeDirsAsync(TemporaryRemoteBaseDirectory, ct);
                    await afc.SetFileContentsAsync(fname, ipaContents, ct);
                }
                else {
                    await UploadIpccFromPathAsync(packagePath, fname, afc, ct);
                }

                await SendPackageAsync(cmd, options, handler, fname, ct);
            }
            finally {
                await afc.RemoveSingleAsync(fname, force: true, cancellationToken: ct);
            }
        }
    }

    /// <summary>
    /// Look up installed apps via the <c>"Lookup"</c> command. The options (e.g. <c>BundleIDs</c>,
    /// <c>ApplicationType</c>, <c>ReturnAttributes</c>) determine which apps and attributes are returned.
    /// </summary>
    /// <returns>The <c>LookupResult</c> mapping bundle identifiers to per-app info, or <c>null</c> if absent.</returns>
    public async Task<DictionaryNode?> LookupAsync(DictionaryNode? options = null, CancellationToken ct = default) {
        DictionaryNode cmd = new DictionaryNode {
            ["Command"] = new StringNode("Lookup"),
            ["ClientOptions"] = options ?? []
        };

        if (Service is null) {
            throw new InstallationProxyException("Service is null");
        }

        PropertyNode? result = await Service.SendReceivePlistAsync(cmd, cancellationToken: ct).ConfigureAwait(false);
        DictionaryNode dict = result?.AsDictionaryNode() ?? [];

        return dict.TryGetValue("LookupResult", out PropertyNode? value) && value is not null ? value.AsDictionaryNode() : null;
    }

    /// <summary>Restore a previously archived app, identified by its bundle identifier.</summary>
    public Task RestoreAsync(
        string bundleIdentifier,
        DictionaryNode? options = null,
        Action<int>? handler = null,
        CancellationToken ct = default)
        => SendCmdForBundleIdentifierAsync(bundleIdentifier, "Restore", options, handler, ct);

    /// <summary>Uninstall an app, identified by its bundle identifier.</summary>
    public Task UninstallAsync(
        string bundleIdentifier,
        DictionaryNode? options = null,
        Action<int>? handler = null,
        CancellationToken ct = default
    ) => SendCmdForBundleIdentifierAsync(bundleIdentifier, "Uninstall", options, handler, ct);

    /// <summary>Upgrade an installed app from a local package (<c>.ipa</c>, <c>.ipcc</c> or app directory).</summary>
    public Task UpgradeAsync(
        string ipaPath,
        DictionaryNode? options = null,
        Action<int>? handler = null,
        CancellationToken ct = default)
        => InstallFromLocalAsync(ipaPath, "Upgrade", options, handler, developer: false, ct);
}

using Microsoft.Extensions.Logging;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Plist;
using Netimobiledevice.Services.Misagent;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Services;

/// <summary>
/// Manage provisioning profiles installed on the device.
/// </summary>
public sealed class MisagentService(
    LockdownServiceProvider lockdown,
    ILogger? logger = null
) : LockdownService(
    lockdown,
    lockdown is LockdownClient ? LockdownServiceName : RemoteServiceName,
    logger: logger
) {
    private const string LockdownServiceName = "com.apple.misagent";
    private const string RemoteServiceName = "com.apple.misagent.shim.remote";

    private static ReadOnlySpan<byte> PlistEnd => "</plist>"u8;
    private static ReadOnlySpan<byte> XmlStart => "<?xml"u8;

    private static async Task<IReadOnlyList<DictionaryNode>> ParseProfiles(ArrayNode rawProfiles) {
        List<DictionaryNode> parsedProfiles = [];
        foreach (PropertyNode profileNode in rawProfiles) {
            if (profileNode is not DataNode profile) {
                continue;
            }

            byte[] buffer = profile.Value;

            ReadOnlySpan<byte> span = buffer;
            int start = span.IndexOf(XmlStart);
            if (start < 0) {
                throw new MisagentException("Provisioning profile does not contain an embedded XML plist.");
            }

            // Take everything from "<?xml" up to the first "</plist>", then re-append "</plist>"
            ReadOnlySpan<byte> xml = span[start..];
            int end = xml.IndexOf(PlistEnd);
            if (end >= 0) {
                xml = xml[..end];
            }

            byte[] plistBytes = new byte[xml.Length + PlistEnd.Length];
            xml.CopyTo(plistBytes);
            PlistEnd.CopyTo(plistBytes.AsSpan(xml.Length));

            PropertyNode plist = await PropertyList.LoadFromByteArrayAsync(plistBytes);
            if (plist is not DictionaryNode dict) {
                throw new MisagentException("Embedded provisioning profile plist is not a dictionary.");
            }
            parsedProfiles.Add(dict);
        }
        return parsedProfiles;
    }

    private async Task<DictionaryNode> SendReceiveRequest(PropertyNode request, CancellationToken cancellationToken) {
        if (Service is null) {
            throw new MisagentException("Service is null");
        }

        PropertyNode? response = await Service.SendReceivePlistAsync(request, cancellationToken).ConfigureAwait(false);
        DictionaryNode responseDict = response?.AsDictionaryNode() ?? [];

        if (!responseDict.TryGetValue("Status", out PropertyNode? statusNode) ||
            statusNode is not IntegerNode status ||
            status.Value != 0
        ) {
            throw new MisagentException($"Invalid status: {PropertyList.SaveAsString(responseDict, PlistFormat.Xml)}");
        }
        return responseDict;
    }

    /// <summary>Retrieve all provisioning profiles installed on the device.</summary>
    /// <returns>One <see cref="ProvisioningProfile"/> per installed profile.</returns>
    /// <exception cref="PyMobileDevice3Exception">The device reports a non-zero status.</exception>
    public async Task<IReadOnlyList<DictionaryNode>> CopyAllAsync(CancellationToken cancellationToken = default) {
        DictionaryNode request = new DictionaryNode() {
            { "MessageType", new StringNode("CopyAll") },
            { "ProfileType", new StringNode("Provisioning") }
        };

        DictionaryNode response = await SendReceiveRequest(request, cancellationToken);
        if (!response.TryGetValue("Payload", out PropertyNode? payload) || payload is not ArrayNode items) {
            throw new MisagentException($"invalid payload: {PropertyList.SaveAsString(response, PlistFormat.Xml)}");
        }

        return await ParseProfiles(items);
    }

    /// <summary>Install a provisioning profile on the device.</summary>
    /// <param name="plist">The raw byte data of the provisioning profile to install; sent to the device.</param>
    /// <returns>The device's response plist.</returns>
    public async Task<DictionaryNode> InstallAsync(DataNode plist, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(plist);
        DictionaryNode request = new DictionaryNode() {
            { "MessageType", new StringNode("Install") },
            { "Profile", plist },
            { "ProfileType", new StringNode("Provisioning") }
        };
        return await SendReceiveRequest(request, cancellationToken);
    }

    /// <summary>Remove an installed provisioning profile.</summary>
    /// <param name="profileId">Identifier of the profile to remove.</param>
    /// <returns>The device's response plist.</returns>
    public async Task<DictionaryNode> RemoveAsync(string profileId, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(profileId);
        DictionaryNode request = new DictionaryNode() {
            { "MessageType", new StringNode("Remove") },
            { "ProfileID", new StringNode(profileId) },
            { "ProfileType", new StringNode("Provisioning") }
        };
        return await SendReceiveRequest(request, cancellationToken);
    }
}

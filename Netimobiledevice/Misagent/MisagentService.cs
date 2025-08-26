using Microsoft.Extensions.Logging;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Plist;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Misagent;

/// <summary>
/// Management for provisioning profiles 
/// </summary>
/// <param name="lockdown"></param>
/// <param name="logger"></param>
public class MisagentService(LockdownServiceProvider lockdown, ILogger? logger = null) : LockdownService(lockdown, LOCKDOWN_SERVICE_NAME, RSD_SERVICE_NAME, logger: logger)
{
    private const string LOCKDOWN_SERVICE_NAME = "com.apple.misagent";
    private const string RSD_SERVICE_NAME = "com.apple.misagent.shim.remote";

    private static async Task<List<PropertyNode>> ParseProfiles(ArrayNode rawProfiles)
    {
        List<PropertyNode> parsedProfiles = [];
        foreach (DataNode profile in rawProfiles.Cast<DataNode>()) {
            byte[] buf = profile.Value;

            byte[] filteredBuffer = [.. SplitArray(buf, Encoding.UTF8.GetBytes("<?xml"))[1]];
            filteredBuffer = [.. SplitArray(filteredBuffer, Encoding.UTF8.GetBytes("</plist>"))[0]];

            List<byte> xmlList =
            [
                .. Encoding.UTF8.GetBytes("<?xml"),
                .. filteredBuffer,
                .. Encoding.UTF8.GetBytes("</plist>"),
            ];

            PropertyNode parsedProfile = await PropertyList.LoadFromByteArrayAsync([.. xmlList]).ConfigureAwait(false);
            parsedProfiles.Add(parsedProfile);
        }
        return parsedProfiles;
    }

    private async Task<DictionaryNode> SendReceiveRequest(PropertyNode request, CancellationToken cancellationToken = default)
    {
        PropertyNode? response = await Service.SendReceivePlistAsync(request, cancellationToken).ConfigureAwait(false);
        DictionaryNode? dict = response?.AsDictionaryNode();
        if (dict != null) {
            if (dict.ContainsKey("Status") && dict["Status"].AsIntegerNode().Value != 0) {
                throw new MisagentException($"Status Error response: {dict["Status"].AsIntegerNode().Value}");
            }
            return dict;
        }
        throw new MisagentException("Missing response from misagent service request");
    }

    private static List<ArraySegment<byte>> SplitArray(byte[] arr, byte[] delimiter)
    {
        List<ArraySegment<byte>> result = [];
        int segStart = 0;
        for (int i = 0, j = 0; i < arr.Length; i++) {
            if (arr[i] != delimiter[j]) {
                if (j == 0) {
                    continue;
                }
                j = 0;
            }

            if (arr[i] == delimiter[j]) {
                j++;
            }

            if (j == delimiter.Length) {
                int segLen = i + 1 - segStart - delimiter.Length;
                if (segLen > 0) {
                    result.Add(new ArraySegment<byte>(arr, segStart, segLen));
                }
                segStart = i + 1;
                j = 0;
            }
        }

        if (segStart < arr.Length) {
            result.Add(new ArraySegment<byte>(arr, segStart, arr.Length - segStart));
        }

        return result;
    }

    public async Task<List<PropertyNode>> GetInstalledProvisioningProfiles()
    {
        DictionaryNode request = new DictionaryNode() {
            { "MessageType", new StringNode("CopyAll") },
            { "ProfileType", new StringNode("Provisioning") }
        };
        DictionaryNode response = await SendReceiveRequest(request).ConfigureAwait(false);
        List<PropertyNode> profiles = await ParseProfiles(response["Payload"].AsArrayNode()).ConfigureAwait(false);
        return profiles;
    }

    public async Task<DictionaryNode> Install(PropertyNode plist)
    {
        DictionaryNode request = new DictionaryNode() {
            { "MessageType", new StringNode("Remove") },
            { "Profile", plist },
            { "ProfileType", new StringNode("Provisioning") }
        };
        DictionaryNode response = await SendReceiveRequest(request).ConfigureAwait(false);
        return response;
    }

    public async Task<DictionaryNode> Uninstall(string profileId)
    {
        DictionaryNode request = new DictionaryNode() {
            { "MessageType", new StringNode("Remove") },
            { "ProfileID", new StringNode(profileId) },
            { "ProfileType", new StringNode("Provisioning") }
        };
        DictionaryNode response = await SendReceiveRequest(request).ConfigureAwait(false);
        return response;
    }
}

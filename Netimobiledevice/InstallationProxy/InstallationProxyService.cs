using Microsoft.Extensions.Logging;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Lockdown.Services;
using Netimobiledevice.Plist;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.InstallationProxy
{
    public sealed class InstallationProxyService : BaseService
    {
        protected override string ServiceName => "com.apple.mobile.installation_proxy";

        public InstallationProxyService(LockdownClient client) : base(client) { }

        public async Task<ArrayNode> Browse(DictionaryNode? options = null, ArrayNode? attributes = null, CancellationToken cancellationToken = default)
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
                PropertyNode? response = await Service.ReceivePlistAsync(cancellationToken);
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

        /// <summary>
        /// Uninstalls the App with the given bundle identifier
        /// </summary>
        /// <param name="bundleIdentifier"></param>
        /// <param name="options"></param>
        /// <param name="callback"></param>
        /// <param name=""></param>
        /// <returns></returns>
        public async Task Uninstall(string bundleIdentifier, CancellationToken cancellationToken, DictionaryNode? options = null, Action<int>? callback = null)
        {
            DictionaryNode cmd = new DictionaryNode() {
                { "Command", new StringNode("Uninstall") },
                { "ApplicationIdentifier", new StringNode(bundleIdentifier) }
            };

            options ??= new DictionaryNode();
            cmd.Add("ClientOptions", options);

            await Service.SendPlistAsync(cmd, cancellationToken).ConfigureAwait(false);

            // Wait for the uninstall to complete
            // TODO self._watch_completion(handler, *args)

            while (true) {
                PropertyNode? response = await Service.ReceivePlistAsync(cancellationToken).ConfigureAwait(false);
                if (response == null) {
                    break;
                }

                DictionaryNode responseDict = response.AsDictionaryNode();
                if (responseDict.TryGetValue("Error", out PropertyNode? errorNode)) {
                    throw new AppInstallException($"{errorNode.AsStringNode().Value}: {responseDict["ErrorDescription"].AsStringNode().Value}");
                }

                if (responseDict.TryGetValue("PercentComplete", out PropertyNode? completion)) {
                    if (callback is not null) {
                        Logger.LogDebug("Using callback");
                        callback((int) completion.AsIntegerNode().Value);
                    }
                    Logger.LogInformation("Uninstall {percentComplete}% Complete", completion.AsIntegerNode().Value);
                }

                if (responseDict.TryGetValue("Status", out PropertyNode? status)) {
                    if (status.AsStringNode().Value == "Complete") {
                        return;
                    }
                }
            }
            throw new AppInstallException();
        }
    }
}

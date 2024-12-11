using Microsoft.Extensions.Logging;
using Netimobiledevice.Afc;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Plist;
using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.InstallationProxy
{
    public sealed class InstallationProxyService(LockdownServiceProvider lockdown, ILogger? logger = null) : LockdownService(lockdown, LOCKDOWN_SERVICE_NAME, RSD_SERVICE_NAME, logger: logger)
    {
        private const string LOCKDOWN_SERVICE_NAME = "com.apple.mobile.installation_proxy";
        private const string RSD_SERVICE_NAME = "com.apple.mobile.installation_proxy.shim.remote";

        private const string TEMP_REMOTE_IPA_FILE = "/netimobiledevice.ipa";

        private static byte[] CreateIpaFromDirectory(string directory)
        {
            string payloadPrefix = "Payload/" + Path.GetFileName(directory);
            byte[] ipaContents;

            // Create a temporary directory
            string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDir);

            try {
                // Create the zip file in the temporary directory
                string zipPath = Path.Combine(tempDir, ".ipa");

                using (ZipArchive zip = ZipFile.Open(zipPath, ZipArchiveMode.Create)) {
                    foreach (string file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)) {
                        string relativePath = Path.GetRelativePath(directory, file);
                        string zipEntryName = Path.Combine(payloadPrefix, relativePath).Replace("\\", "/");

                        zip.CreateEntryFromFile(file, zipEntryName);
                    }
                }

                // Read the contents of the zip file into a byte array
                ipaContents = File.ReadAllBytes(zipPath);
            }
            finally {
                // Clean up the temporary directory
                if (Directory.Exists(tempDir)) {
                    Directory.Delete(tempDir, true);
                }
            }

            return ipaContents;
        }

        /// <summary>
        /// Upload given ipa onto device and install/upgrade it
        /// </summary>
        /// <param name="ipaPath"></param>
        /// <param name="command"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="callback"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        private async Task InstallFromLocal(string ipaPath, string command, CancellationToken cancellationToken, DictionaryNode? options = null, IProgress<int>? progress = null)
        {
            options ??= [];

            byte[] ipaContents;
            if (Directory.Exists(ipaPath)) {
                // Treat the directory as an app and convert into an ipa
                ipaContents = CreateIpaFromDirectory(ipaPath);
            }
            else {
                ipaContents = await File.ReadAllBytesAsync(ipaPath, cancellationToken).ConfigureAwait(false);
            }

            using (AfcService afc = new AfcService(Lockdown)) {
                await afc.SetFileContents(TEMP_REMOTE_IPA_FILE, ipaContents, cancellationToken).ConfigureAwait(false);
            }
            Logger.LogInformation("IPA sent to device");

            DictionaryNode cmd = new DictionaryNode
            {
                { "Command", new StringNode(command) },
                { "ClientOptions", options },
                { "PackagePath", new StringNode(TEMP_REMOTE_IPA_FILE) }
            };
            await Service.SendPlistAsync(cmd, cancellationToken: cancellationToken).ConfigureAwait(false);

            await WatchForCompletion(command, cancellationToken, progress);
            Logger?.LogInformation("IPA Installed");
        }

        private async Task WatchForCompletion(string action, CancellationToken cancellationToken, IProgress<int>? progress = null)
        {
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
                    progress?.Report((int) completion.AsIntegerNode().Value);
                    Logger.LogInformation("{action} {percentComplete}% Complete", action, completion.AsIntegerNode().Value);
                }

                if (responseDict.TryGetValue("Status", out PropertyNode? status)) {
                    if (status.AsStringNode().Value == "Complete") {
                        return;
                    }
                }
            }
            throw new AppInstallException("Installation or command did not complete successfully.");
        }

        public async Task<ArrayNode> Browse(DictionaryNode? options = null, ArrayNode? attributes = null, CancellationToken cancellationToken = default)
        {
            options ??= [];
            if (attributes != null) {
                options.Add("ReturnAttributes", attributes);
            }

            DictionaryNode command = new DictionaryNode() {
                {"Command", new StringNode("Browse") },
                {"ClientOptions", options }
            };
            Service.SendPlist(command);

            ArrayNode result = [];
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
        /// Install a given IPA from device path
        /// </summary>
        /// <param name="ipaPath"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="callback"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public async Task Install(string ipaPath, CancellationToken cancellationToken, DictionaryNode? options = null, IProgress<int>? progress = null)
        {
            await InstallFromLocal(ipaPath, "Install", cancellationToken, options, progress);
        }

        /// <summary>
        /// Uninstalls the App with the given bundle identifier
        /// </summary>
        /// <param name="bundleIdentifier"></param>
        /// <param name="options"></param>
        /// <param name="callback"></param>
        /// <param name=""></param>
        /// <returns></returns>
        public async Task Uninstall(string bundleIdentifier, CancellationToken cancellationToken, DictionaryNode? options = null, IProgress<int>? progress = null)
        {
            DictionaryNode cmd = new DictionaryNode() {
                { "Command", new StringNode("Uninstall") },
                { "ApplicationIdentifier", new StringNode(bundleIdentifier) }
            };

            options ??= [];
            cmd.Add("ClientOptions", options);

            await Service.SendPlistAsync(cmd, cancellationToken: cancellationToken).ConfigureAwait(false);
            await WatchForCompletion("Uninstall", cancellationToken, progress).ConfigureAwait(false);
        }

        /// <summary>
        /// Upgrade given ipa from device path
        /// </summary>
        /// <param name="ipaPath"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="callback"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public async Task Upgrade(string ipaPath, CancellationToken cancellationToken, DictionaryNode? options = null, IProgress<int>? progress = null)
        {
            await InstallFromLocal(ipaPath, "Upgrade", cancellationToken, options, progress);
        }
    }
}

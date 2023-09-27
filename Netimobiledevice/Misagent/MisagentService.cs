using Netimobiledevice.Lockdown;
using Netimobiledevice.Lockdown.Services;
using Netimobiledevice.Plist;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netimobiledevice.Misagent
{
    public class MisagentService : BaseService
    {
        protected override string ServiceName => "com.apple.misagent";

        public MisagentService(LockdownClient client) : base(client) { }

        private static List<PropertyNode> ParseProfiles(ArrayNode rawProfiles)
        {
            List<PropertyNode> parsedProfiles = new List<PropertyNode>();
            foreach (DataNode profile in rawProfiles.Cast<DataNode>()) {
                byte[] buf = profile.Value;

                byte[] filteredBuffer = SplitArray(buf, Encoding.UTF8.GetBytes("<?xml"))[1].ToArray();
                filteredBuffer = SplitArray(filteredBuffer, Encoding.UTF8.GetBytes("</plist>"))[0].ToArray();

                List<byte> xmlList = new List<byte>();
                xmlList.AddRange(Encoding.UTF8.GetBytes("<?xml"));
                xmlList.AddRange(filteredBuffer);
                xmlList.AddRange(Encoding.UTF8.GetBytes("</plist>"));

                PropertyNode parsedProfile = PropertyList.LoadFromByteArray(xmlList.ToArray());
                parsedProfiles.Add(parsedProfile);
            }
            return parsedProfiles;
        }

        private async Task<DictionaryNode> SendReceiveRequest(PropertyNode request)
        {
            PropertyNode? response = await Service.SendReceivePlistAsync(request);
            DictionaryNode? dict = response?.AsDictionaryNode();
            if (dict != null) {
                if (dict.ContainsKey("Status") && dict["Status"].AsIntegerNode().Value != 0) {
                    throw new Exception($"Status Error response: {dict["Status"].AsIntegerNode().Value}");
                }
                return dict;
            }
            throw new Exception("Missing response from misagent service request");
        }

        private static List<ArraySegment<byte>> SplitArray(byte[] arr, byte[] delimiter)
        {
            List<ArraySegment<byte>> result = new List<ArraySegment<byte>>();
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
                {"ProfileType", new StringNode("Provisioning") }
            };
            DictionaryNode response = await SendReceiveRequest(request);
            List<PropertyNode> profiles = ParseProfiles(response["Payload"].AsArrayNode());
            return profiles;
        }

        public async Task<DictionaryNode> Install(PropertyNode plist)
        {
            DictionaryNode request = new DictionaryNode() {
                { "MessageType", new StringNode("Remove") },
                {"Profile", plist },
                {"ProfileType", new StringNode("Provisioning") }
            };
            DictionaryNode response = await SendReceiveRequest(request);
            return response;
        }

        public async Task<DictionaryNode> Uninstall(string profileId)
        {
            DictionaryNode request = new DictionaryNode() {
                { "MessageType", new StringNode("Remove") },
                {"ProfileID", new StringNode(profileId) },
                {"ProfileType", new StringNode("Provisioning") }
            };
            DictionaryNode response = await SendReceiveRequest(request);
            return response;
        }
    }
}

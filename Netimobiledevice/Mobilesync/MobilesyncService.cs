using Netimobiledevice.Lockdown;
using Netimobiledevice.Lockdown.Services;
using Netimobiledevice.Plist;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Netimobiledevice.Mobilesync
{
    public sealed class MobilesyncService : BaseService
    {
        private const int MOBILESYNC_VERSION_MAJOR = 400;
        private const int MOBILESYNC_VERSION_MINOR = 100;

        private const string EMPTY_PARAMETER_STRING = "___EmptyParameterString___";
        private const string SERVICE_NAME = "com.apple.mobilesync";

        private ulong computerDataClassVersion = 106;
        private DeviceLink? deviceLink;
        private MobilesyncType syncType;
        private ulong deviceDataClassVersion;
        private string syncingDataClass;

        protected override string ServiceName => SERVICE_NAME;

        private MobilesyncService(LockdownClient client) : base(client) { }

        private void GetRecords(string operation)
        {
            ArrayNode msg = new ArrayNode() {
                new StringNode(operation),
                new StringNode(syncingDataClass)
            };
            deviceLink?.Send(msg);
        }

        private async Task InitialiseAsync()
        {
            deviceLink = new DeviceLink(Service);
            await deviceLink.VersionExchange(MOBILESYNC_VERSION_MAJOR, MOBILESYNC_VERSION_MINOR);
        }

        public void AcknowledgeChangesFromDevice()
        {
            ArrayNode msg = new ArrayNode() {
                new StringNode("SDMessageAcknowledgeChangesFromDevice"),
                new StringNode(syncingDataClass)
            };
            deviceLink?.Send(msg);
        }

        public void GetAllRecordsFromDevice()
        {
            GetRecords("SDMessageGetAllRecordsFromDevice");
        }

        public async Task FinishSync()
        {
            ArrayNode? msg = new ArrayNode() {
                new StringNode("SDMessageFinishSessionOnDevice"),
                new StringNode(syncingDataClass)
            };
            deviceLink?.Send(msg);

            msg = null;
            if (deviceLink != null) {
                msg = await deviceLink.ReceiveMessage();
            }

            if (msg != null) {
                string responseType = msg[0].AsStringNode().Value;
                if (!string.IsNullOrEmpty(responseType) && responseType != "SDMessageDeviceFinishedSession") {
                    throw new Exception($"Device failed to finish sync: {responseType}");
                }
            }
        }

        public async IAsyncEnumerable<PropertyNode> ReceiveChanges(PropertyNode? actions)
        {
            bool isLastRecord = false;
            if (deviceLink != null) {
                while (!isLastRecord) {
                    ArrayNode msg = await deviceLink.ReceiveMessage();
                    if (msg != null) {
                        string responseType = msg[0].AsStringNode().Value;

                        if (responseType == "SDMessageCancelSession") {
                            string reason = msg[2].AsStringNode().Value;
                            Debug.WriteLine($"mobilesync cancelled by device: {reason}");
                            yield break;
                        }

                        if (!isLastRecord) {
                            bool hasMoreChanges = msg[3].AsBooleanNode().Value;
                            isLastRecord = !hasMoreChanges;
                        }

                        if (actions != null) {
                            PropertyNode actionsNode = msg[4];
                            if (actionsNode.NodeType == PlistType.Dict) {
                                actions = actionsNode;
                            }
                        }

                        yield return msg[2];
                    }
                }
            }
        }

        public async Task StartSync(string dataClass, MobilesyncAnchors anchors)
        {
            ArrayNode? msg = new ArrayNode() {
                new StringNode("SDMessageSyncDataClassWithDevice"),
                new StringNode(dataClass)
            };
            if (!string.IsNullOrEmpty(anchors.DeviceAnchor)) {
                msg.Add(new StringNode(anchors.DeviceAnchor));
            }
            else {
                msg.Add(new StringNode("---"));
            }
            msg.Add(new StringNode(anchors.ComputerAnchor));
            msg.Add(new IntegerNode(computerDataClassVersion));
            msg.Add(new StringNode(EMPTY_PARAMETER_STRING));
            deviceLink?.Send(msg);

            msg = null;
            if (deviceLink != null) {
                msg = await deviceLink.ReceiveMessage();
            }

            if (msg != null) {
                string responseType = msg[0].AsStringNode().Value;

                // Did the device refuse to sync with the computer?
                if (responseType == "SDMessageRefuseToSyncDataClassWithComputer") {
                    string errorDescription = msg[2].AsStringNode().Value;
                    throw new Exception($"Device refused to sync: {errorDescription}");
                }

                // Did the device cancel the session?
                if (responseType == "SDMessageCancelSession") {
                    string errorDescription = msg[2].AsStringNode().Value;
                    throw new Exception($"Device cancelled sync: {errorDescription}");
                }

                string syncTypeString = msg[4].AsStringNode().Value;
                bool success = Enum.TryParse(syncTypeString, true, out MobilesyncType mobileSyncType);
                if (success) {
                    syncType = mobileSyncType;
                }
                else {
                    throw new Exception("Unknown mobilesync type");
                }

                deviceDataClassVersion = msg[5].AsIntegerNode().Value;
                syncingDataClass = dataClass;
            }
        }

        public static async Task<MobilesyncService> StartServiceAsync(LockdownClient client)
        {
            MobilesyncService service = new MobilesyncService(client);
            await service.InitialiseAsync();
            return service;
        }
    }
}

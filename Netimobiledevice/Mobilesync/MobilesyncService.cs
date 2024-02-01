using Microsoft.Extensions.Logging;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Lockdown.Services;
using Netimobiledevice.Plist;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Mobilesync
{
    public sealed class MobilesyncService : DeviceLink
    {
        private const int MOBILESYNC_VERSION_MAJOR = 400;
        private const int MOBILESYNC_VERSION_MINOR = 100;

        private const ulong COMPUTER_DATA_CLASS_VERSION = 106;
        private const string EMPTY_PARAMETER_STRING = "___EmptyParameterString___";
        private const string SERVICE_NAME = "com.apple.mobilesync";

        /// <summary>
        /// The internal logger
        /// </summary>
        private readonly ILogger logger;

        private string syncingDataClass = EMPTY_PARAMETER_STRING;

        protected override string ServiceName => SERVICE_NAME;

        private MobilesyncService(LockdownClient client, ILogger logger) : base(client)
        {
            this.logger = logger;
        }

        private void GetRecords(string operation)
        {
            ArrayNode msg = new ArrayNode() {
                new StringNode(operation),
                new StringNode(syncingDataClass)
            };
            DeviceLinkSend(msg);
        }

        private ArrayNode CreateProcessChangesMessage(DictionaryNode entities, bool moreChanges, PropertyNode? actions)
        {
            ArrayNode msg = new ArrayNode() {
                new StringNode("SDMessageProcessChanges"),
                new StringNode(syncingDataClass),
                entities,
                new BooleanNode(moreChanges)
            };

            if (actions != null) {
                msg.Add(actions);
            }
            else {
                msg.Add(new StringNode(EMPTY_PARAMETER_STRING));
            }

            return msg;
        }

        public void AcknowledgeChangesFromDevice()
        {
            ArrayNode msg = new ArrayNode() {
                new StringNode("SDMessageAcknowledgeChangesFromDevice"),
                new StringNode(syncingDataClass)
            };
            DeviceLinkSend(msg);
        }

        public void GetAllRecordsFromDevice()
        {
            GetRecords("SDMessageGetAllRecordsFromDevice");
        }

        public async Task FinishSync(CancellationToken cancellationToken = default)
        {
            ArrayNode? msg = new ArrayNode() {
                new StringNode("SDMessageFinishSessionOnDevice"),
                new StringNode(syncingDataClass)
            };
            DeviceLinkSend(msg);

            msg = await DeviceLinkReceiveMessage(cancellationToken);
            string responseType = msg[0].AsStringNode().Value;
            if (!string.IsNullOrEmpty(responseType) && responseType != "SDMessageDeviceFinishedSession") {
                throw new Exception($"Device failed to finish sync: {responseType}");
            }
        }

        public async Task ReadyToSendChangesFromComputer(CancellationToken cancellationToken = default)
        {
            ArrayNode msg = await DeviceLinkReceiveMessage(cancellationToken);

            string responseType = msg[0].AsStringNode().Value;
            if (responseType == "SDMessageCancelSession") {
                string reason = msg[2].AsStringNode().Value;
                throw new Exception($"Device cancelled sync: {reason}");
            }

            if (responseType != "SDMessageDeviceReadyToReceiveChanges") {
                throw new Exception("Device not ready");
            }

            DeviceLinkSendPing("Preparing to get changes for device");
        }

        public async IAsyncEnumerable<PropertyNode> ReceiveChanges(PropertyNode? actions, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            bool isLastRecord = false;
            while (!isLastRecord) {
                ArrayNode msg = await DeviceLinkReceiveMessage(cancellationToken);
                if (msg != null) {
                    string responseType = msg[0].AsStringNode().Value;

                    if (responseType == "SDMessageCancelSession") {
                        string reason = msg[2].AsStringNode().Value;
                        logger.LogWarning($"mobilesync cancelled by device: {reason}");
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

        public async Task<DictionaryNode> RemapIdentifiers(CancellationToken cancellationToken = default)
        {
            ArrayNode msg = await DeviceLinkReceiveMessage(cancellationToken);

            string responseType = msg[0].AsStringNode().Value;
            if (responseType == "SDMessageCancelSession") {
                string reason = msg[2].AsStringNode().Value;
                throw new Exception($"Device cancelled sync: {reason}");
            }

            if (responseType != "SDMessageRemapRecordIdentifiers") {
                throw new Exception("Device not remapping");
            }

            if (msg[2].NodeType == PlistType.Dict) {
                DictionaryNode mapping = msg[2].AsDictionaryNode();
                return mapping;
            }
            return new DictionaryNode();
        }

        public void SendChanges(DictionaryNode entities, bool isLastRecord, PropertyNode? actions)
        {
            ArrayNode msg = CreateProcessChangesMessage(entities, isLastRecord, actions);
            DeviceLinkSend(msg);
        }

        public async Task StartSync(string dataClass, MobilesyncAnchors anchors, CancellationToken cancellationToken = default)
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
            msg.Add(new IntegerNode(COMPUTER_DATA_CLASS_VERSION));
            msg.Add(new StringNode(EMPTY_PARAMETER_STRING));
            DeviceLinkSend(msg);

            msg = await DeviceLinkReceiveMessage(cancellationToken);
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

            syncingDataClass = dataClass;
        }

        public static async Task<MobilesyncService> StartServiceAsync(LockdownClient client, ILogger logger, CancellationToken cancellationToken = default)
        {
            MobilesyncService service = new MobilesyncService(client, logger);
            await service.DeviceLinkVersionExchange(MOBILESYNC_VERSION_MAJOR, MOBILESYNC_VERSION_MINOR, cancellationToken);
            return service;
        }
    }
}

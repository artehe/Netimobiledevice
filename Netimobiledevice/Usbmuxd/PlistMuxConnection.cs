using Netimobiledevice.EndianBitConversion;
using Netimobiledevice.Exceptions;
using Netimobiledevice.Plist;
using Netimobiledevice.Usbmuxd.Responses;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Netimobiledevice.Usbmuxd
{
    internal class PlistMuxConnection : UsbmuxConnection
    {
        private const string PLIST_CLIENT_VERSION_STRING = "1.0.0.0";
        private const int PLIST_USBMUX_VERSION = 3;

        public PlistMuxConnection(UsbmuxdSocket sock) : base(sock, UsbmuxdVersion.Plist) { }

        private static PropertyNode CreatePlistMessage(string messageType)
        {
            string bundleId = GetBundleId();
            string assemblyName = GetAssemblyName();

            DictionaryNode plistDict = new DictionaryNode();
            if (!string.IsNullOrWhiteSpace(bundleId)) {
                plistDict.Add("BundleID", new StringNode(bundleId));
            }
            plistDict.Add("ClientVersionString", new StringNode(PLIST_CLIENT_VERSION_STRING));
            plistDict.Add("MessageType", new StringNode(messageType));
            if (!string.IsNullOrWhiteSpace(assemblyName)) {
                plistDict.Add("ProgName", new StringNode(assemblyName));
            }
            plistDict.Add("kLibUSBMuxVersion", new IntegerNode(PLIST_USBMUX_VERSION));
            return plistDict;
        }

        private static string GetAssemblyName()
        {
            AssemblyName? progName = Assembly.GetEntryAssembly()?.GetName();
            return progName?.Name ?? "Netimobiledevice";
        }

        private static string GetBundleId()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                string executeablePath = Assembly.GetExecutingAssembly().Location;
                string executeableDir = Path.GetDirectoryName(executeablePath) ?? string.Empty;
                string infoPlistPath = Path.Combine(executeableDir, "..", "Info.plist");

                if (File.Exists(infoPlistPath)) {
                    using (FileStream fileStream = File.OpenRead(infoPlistPath)) {
                        DictionaryNode infoPlistDict = PropertyList.Load(fileStream).AsDictionaryNode();
                        string bundleId = infoPlistDict["CFBundleIdentifier"].AsStringNode().Value;
                        return bundleId;
                    }
                }
            }
            return string.Empty;
        }

        private UsbmuxdResult SendReceive(PropertyNode msg)
        {
            Send(msg);

            PlistResponse response = ReceivePlist(Tag - 1);
            DictionaryNode respPlist = response.Plist.AsDictionaryNode();

            string msgType = respPlist["MessageType"].AsStringNode().Value;
            if (msgType != "Result") {
                throw new UsbmuxException($"Got an invalid message: {response}");
            }

            UsbmuxdResult result = (UsbmuxdResult) respPlist["Number"].AsIntegerNode().Value;
            if (result != UsbmuxdResult.Ok) {
                throw new UsbmuxException($"Got an error message: {response}");
            }
            return result;
        }

        protected override void RequestConnect(ulong deviceId, ushort port)
        {
            DictionaryNode dict = new DictionaryNode {
                { "MessageType", new StringNode("Connect") },
                { "DeviceID", new IntegerNode(deviceId) },
                { "PortNumber", new IntegerNode(EndianNetworkConverter.HostToNetworkOrder(port)) }
            };
            SendReceive(dict);
        }

        public DictionaryNode GetPairRecord(string serial)
        {
            // Serials are saved inside usbmuxd without '-'
            DictionaryNode message = CreatePlistMessage("ReadPairRecord").AsDictionaryNode();
            message.Add("PairRecordID", new StringNode(serial));
            Send(message);

            DictionaryNode response = ReceivePlist(Tag - 1).Plist.AsDictionaryNode();
            if (response.ContainsKey("PairRecordData")) {
                byte[] pairRecordData = response["PairRecordData"].AsDataNode().Value;
                return PropertyList.LoadFromByteArray(pairRecordData).AsDictionaryNode();
            }
            else {
                throw new NotPairedException();
            }
        }

        public override UsbmuxdResult Listen()
        {
            connectionTimeout = -1;
            Sock.SetTimeout(connectionTimeout);

            PropertyNode plistMessage = CreatePlistMessage("Listen");
            return SendReceive(plistMessage);
        }

        public PlistResponse ReceivePlist(int expectedTag)
        {
            (UsbmuxdHeader header, byte[] payload) = Receive(expectedTag);
            if (header.Message != UsbmuxdMessageType.Plist) {
                throw new UsbmuxException($"Received non-plist type {header}");
            }

            PlistResponse response = new PlistResponse(header, payload);
            return response;
        }

        public void SavePairRecord(string serial, int deviceId, byte[] recordData)
        {
            // Serials are saved inside usbmuxd without '-'
            DictionaryNode message = new DictionaryNode {
                { "MessageType", new StringNode("SavePairRecord") },
                { "PairRecordID", new StringNode(serial) },
                { "PairRecordData", new DataNode(recordData) },
                { "DeviceID", new IntegerNode(deviceId) }
            };
            SendReceive(message);
        }

        public int Send(PropertyNode msg)
        {
            byte[] payload = PropertyList.SaveAsByteArray(msg, PlistFormat.Xml).ToArray();
            return SendPacket(UsbmuxdMessageType.Plist, Tag, payload);
        }

        public override void UpdateDeviceList(int timeout = 5000)
        {
            // Get the list of devices synchronously without waiting for the timeout
            Devices.Clear();
            PropertyNode plistMessage = CreatePlistMessage("ListDevices");
            Send(plistMessage);

            PlistResponse response = ReceivePlist(Tag - 1);
            DictionaryNode responseDict = response.Plist.AsDictionaryNode();
            ArrayNode deviceListPlist = responseDict["DeviceList"].AsArrayNode();
            foreach (PropertyNode entry in deviceListPlist) {
                DictionaryNode dict = entry.AsDictionaryNode();
                string messageType = dict["MessageType"].AsStringNode().Value;
                if (messageType == "Attached") {
                    AddDevice(new UsbmuxdDevice(dict["DeviceID"].AsIntegerNode(), dict["Properties"].AsDictionaryNode()));
                }
                else if (messageType == "Detached") {
                    RemoveDevice(dict["DeviceID"].AsIntegerNode().Value);
                }
                else {
                    throw new UsbmuxException($"Invalid packet type received: {entry}");
                }
            }
        }
    }
}

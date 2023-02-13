using Netimobiledevice.Exceptions;
using Netimobiledevice.Plist;
using Netimobiledevice.Usbmuxd.Responses;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Netimobiledevice.Usbmuxd
{
    internal class PlistMuxConnection : UsbmuxConnection
    {
        private const string PLIST_CLIENT_VERSION_STRING = "1.0.0.0";
        private const int PLIST_USBMUX_VERSION = 3;

        public PlistMuxConnection(UsbmuxdSocket sock) : base(sock) { }

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
                string executeablePath = Assembly.GetEntryAssembly().Location;
                string executeableDir = Path.GetDirectoryName(executeablePath);
                string infoPlistPath = executeableDir.Replace("MacOS/", "Info.plist");

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
            Sock.SendPlistPacket(Tag, msg);
            Tag++;

            PlistResponse response = Sock.ReceivePlistResponse(Tag - 1);
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

        public override UsbmuxdResult Listen()
        {
            Sock.SetTimeout(-1);

            PropertyNode plistMessage = CreatePlistMessage("Listen");
            return SendReceive(plistMessage);
        }

        public override void UpdateDeviceList(int timeout = 5000)
        {
            // Get the list of devices synchronously without waiting for the timeout
            Devices.Clear();
            PropertyNode plistMessage = CreatePlistMessage("ListDevices");
            Sock.SendPlistPacket(Tag, plistMessage);
            Tag++;

            PlistResponse response = Sock.ReceivePlistResponse(Tag - 1);
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

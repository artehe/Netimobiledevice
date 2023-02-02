using Netimobiledevice.Exceptions;
using Netimobiledevice.Plist;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Netimobiledevice.Usbmuxd;

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

    public override void UpdateDeviceList(int timeout = 5000)
    {
        // Get the list of devices synchronously without waiting for the timeout
        Devices.Clear();
        PropertyNode plistMessage = CreatePlistMessage("ListDevices");
        Sock.SendPlistPacket(Tag, plistMessage);
        Tag++;

        UsbmuxdResponse response = Sock.ReceivePlistResponse(Tag - 1);
        PropertyNode deviceListPlist = response.Data.AsDictionaryNode()["DeviceList"];
        foreach (PropertyNode entry in deviceListPlist.AsArrayNode()) {
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

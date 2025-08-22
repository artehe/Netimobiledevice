using Microsoft.Extensions.Logging;
using Netimobiledevice.EndianBitConversion;
using Netimobiledevice.Exceptions;
using Netimobiledevice.Plist;
using Netimobiledevice.Usbmuxd.Responses;
using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Usbmuxd;

internal class PlistMuxConnection(UsbmuxdSocket sock, ILogger? logger = null) : UsbmuxConnection(sock, UsbmuxdVersion.Plist, logger)
{
    private const string PLIST_CLIENT_VERSION_STRING = "1.0.0.0";
    private const int PLIST_USBMUX_VERSION = 3;

    private static DictionaryNode CreatePlistMessage(string messageType)
    {
        string bundleId = GetBundleId();
        string assemblyName = GetAssemblyName();

        DictionaryNode plistDict = [];
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
        if (OperatingSystem.IsMacOS()) {
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

    private async Task<UsbmuxdResult> SendReceiveAsync(PropertyNode msg, CancellationToken cancellationToken = default)
    {
        await SendAsync(msg, cancellationToken).ConfigureAwait(false);

        PlistResponse response = await ReceivePlistAsync(Tag - 1, cancellationToken).ConfigureAwait(false);
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

    protected override async Task RequestConnect(long deviceId, ushort port, CancellationToken cancellationToken = default)
    {
        DictionaryNode dict = new DictionaryNode {
            { "MessageType", new StringNode("Connect") },
            { "DeviceID", new IntegerNode(deviceId) },
            { "PortNumber", new IntegerNode(EndianNetworkConverter.HostToNetworkOrder(port)) }
        };
        await SendReceiveAsync(dict, cancellationToken).ConfigureAwait(false);
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

    /// <summary>
    /// get SystemBUID
    /// </summary>
    /// <returns></returns>
    public string GetBuid()
    {
        DictionaryNode msg = new DictionaryNode() {
            { "MessageType", new StringNode("ReadBUID") }
        };
        Send(msg);
        return ReceivePlist(Tag - 1).Plist.AsDictionaryNode()["BUID"].AsStringNode().Value;
    }

    public override UsbmuxdResult Listen()
    {
        connectionTimeout = -1;
        Sock.SetTimeout(connectionTimeout);
        PropertyNode plistMessage = CreatePlistMessage("Listen");
        return SendReceive(plistMessage);
    }

    public override async Task<UsbmuxdResult> ListenAsync(CancellationToken cancellationToken = default)
    {
        connectionTimeout = -1;
        Sock.SetTimeout(connectionTimeout);
        PropertyNode plistMessage = CreatePlistMessage("Listen");
        return await SendReceiveAsync(plistMessage, cancellationToken).ConfigureAwait(false);
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

    public async Task<PlistResponse> ReceivePlistAsync(int expectedTag, CancellationToken cancellationToken = default)
    {
        UsbmuxPacket packet = await ReceiveAsync(expectedTag, cancellationToken).ConfigureAwait(false);
        if (packet.Header.Message != UsbmuxdMessageType.Plist) {
            throw new UsbmuxException($"Received non-plist type {packet.Header}");
        }

        PlistResponse response = new PlistResponse(packet.Header, packet.Payload);
        return response;
    }

    public void SavePairRecord(string identifier, long deviceId, byte[] recordData)
    {
        // Serials are saved inside usbmuxd without '-'
        DictionaryNode message = new DictionaryNode {
            { "MessageType", new StringNode("SavePairRecord") },
            { "PairRecordID", new StringNode(identifier) },
            { "PairRecordData", new DataNode(recordData) },
            { "DeviceID", new IntegerNode(deviceId) }
        };
        SendReceive(message);
    }

    public int Send(PropertyNode msg)
    {
        byte[] payload = PropertyList.SaveAsByteArray(msg, PlistFormat.Xml);
        return SendPacket(UsbmuxdMessageType.Plist, Tag, payload);
    }

    public async Task<int> SendAsync(PropertyNode msg, CancellationToken cancellationToken = default)
    {
        byte[] payload = PropertyList.SaveAsByteArray(msg, PlistFormat.Xml);
        return await SendPacketAsync(UsbmuxdMessageType.Plist, Tag, payload, cancellationToken).ConfigureAwait(false);
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
                RemoveDevice(dict["DeviceID"].AsIntegerNode().SignedValue);
            }
            else {
                throw new UsbmuxException($"Invalid packet type received: {entry}");
            }
        }
    }
}

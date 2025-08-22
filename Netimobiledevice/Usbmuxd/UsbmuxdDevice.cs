using Microsoft.Extensions.Logging;
using Netimobiledevice.Plist;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Netimobiledevice.Usbmuxd;

/// <summary>
/// Usbmuxd Device information.
/// </summary>
public class UsbmuxdDevice
{
    public UsbmuxdConnectionType ConnectionType { get; private set; } = UsbmuxdConnectionType.None;
    public long DeviceId { get; private set; } = -1;
    public string Serial { get; private set; } = string.Empty;
    public byte[] NetworkAddress { get; private set; } = [];
    public int InterfaceIndex { get; private set; } = -1;

    public UsbmuxdDevice(IntegerNode deviceId, DictionaryNode propertiesDict)
    {
        DeviceId = deviceId.SignedValue;
        Serial = propertiesDict["SerialNumber"].AsStringNode().Value;

        string connectionTypeString = propertiesDict["ConnectionType"].AsStringNode().Value;
        if (connectionTypeString == "USB") {
            ConnectionType = UsbmuxdConnectionType.Usb;
        }
        else if (connectionTypeString == "Network") {
            ConnectionType = UsbmuxdConnectionType.Network;
            DataNode netAddressNode = propertiesDict["NetworkAddress"].AsDataNode();
            IntegerNode netInterfaceIndexNode = propertiesDict["InterfaceIndex"].AsIntegerNode();
            if (netInterfaceIndexNode != null) {
                InterfaceIndex = (int) netInterfaceIndexNode.Value;
            }

            byte addressValue = netAddressNode.Value[1];
            if (OperatingSystem.IsWindows()) {
                addressValue = netAddressNode.Value[0];
            }

            if (addressValue == 2) {
                // AF_INET
                NetworkAddress = [
                    netAddressNode.Value[4],
                    netAddressNode.Value[5],
                    netAddressNode.Value[6],
                    netAddressNode.Value[7]
                ];
            }
            else if (addressValue == 0x1E || addressValue == (int) AddressFamily.InterNetworkV6) {
                // IPV6
                IPAddress ipAddress = new IPAddress(netAddressNode.Value.AsSpan(8, 16));
                NetworkAddress = ipAddress.GetAddressBytes();
            }
            else {
                throw new NotImplementedException($"Network address is not supported. NetAddress Node Array [ {BitConverter.ToString(netAddressNode.Value).Replace("-", ", ")} ]");
            }
        }
        else {
            throw new NotImplementedException($"Unknown connection type: {connectionTypeString}");
        }
    }

    public UsbmuxdDevice(uint deviceId, string serialNumber, UsbmuxdConnectionType connectionType)
    {
        DeviceId = deviceId;
        Serial = serialNumber;
        ConnectionType = connectionType;
    }

    public async Task<Socket> Connect(ushort port, string usbmuxAddress = "", ILogger? logger = null)
    {
        UsbmuxConnection muxConnection = UsbmuxConnection.Create(usbmuxAddress, logger);
        try {
            return await muxConnection.ConnectAsync(this, port).ConfigureAwait(false);
        }
        catch (Exception ex) {
            logger?.LogWarning(ex, "Couldn't connect to port {port}", port);
            muxConnection.Close();
            throw;
        }
    }
}

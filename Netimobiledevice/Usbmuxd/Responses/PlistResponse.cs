using Netimobiledevice.Plist;

namespace Netimobiledevice.Usbmuxd.Responses;

internal readonly struct PlistResponse(UsbmuxdHeader header, byte[] data)
{
    public UsbmuxdHeader Header { get; } = header;
    public PropertyNode Plist { get; } = PropertyList.LoadFromByteArray(data);
}

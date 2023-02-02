using Netimobiledevice.Exceptions;
using Netimobiledevice.Plist;

namespace Netimobiledevice.Usbmuxd;

internal class UsbmuxdResponse
{
    private byte[] Payload { get; }
    private PropertyNode Plist { get; }
    public UsbmuxdHeader Header { get; }
    public dynamic Data {
        get {
            if (Plist != null) {
                return Plist;
            }
            if (Payload != null) {
                return Payload;
            }
            throw new UsbmuxException("Both byte and plist data are null");
        }
    }

    public UsbmuxdResponse(UsbmuxdHeader header, PropertyNode plist)
    {
        Header = header;
        Plist = plist;
    }

    public UsbmuxdResponse(UsbmuxdHeader header, byte[] payload)
    {
        Header = header;
        Payload = payload;
    }
}

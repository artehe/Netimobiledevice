using System.Runtime.InteropServices;

namespace Netimobiledevice.Usbmuxd
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct UsbmuxdHeader
    {
        public int Length; // Length of message including header
        public UsbmuxdVersion Version; // Protocol version
        public UsbmuxdMessageType Message; // Message type
        public int Tag; // Responses to this query will echo back this tag
    }
}

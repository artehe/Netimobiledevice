using System.Runtime.InteropServices;

namespace Netimobiledevice.Usbmuxd;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct UsbmuxdHeader
{
    /// <summary>
    /// Length of message including header
    /// </summary>
    public int Length;
    /// <summary>
    /// Protocol version
    /// </summary>
    public UsbmuxdVersion Version;
    /// <summary>
    /// Message type
    /// </summary>
    public UsbmuxdMessageType Message;
    /// <summary>
    /// Responses to this query will echo back this tag
    /// </summary>
    public int Tag;
}

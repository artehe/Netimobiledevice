using System;

namespace Netimobiledevice.Usbmuxd;

/// <summary>
/// Device lookup options for usbmuxd_get_device.
/// </summary>
[Flags]
public enum UsbmuxdLookupOptions : int
{
    /// <summary>
    /// Include USB connected devices during lookup.
    /// </summary>
    Usb = 1 << 1,
    /// <summary>
    /// Include network connected devices during lookup.
    /// </summary>
    Network = 1 << 2,
    /// <summary>
    /// Prefer network connection.
    /// </summary>
    PreferNetwork = 1 << 3
}

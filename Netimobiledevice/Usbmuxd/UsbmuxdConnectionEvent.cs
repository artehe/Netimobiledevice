namespace Netimobiledevice.Usbmuxd;

public readonly record struct UsbmuxdConnectionEvent(
    UsbmuxdDevice Device,
    UsbmuxdConnectionEventType EventType
);

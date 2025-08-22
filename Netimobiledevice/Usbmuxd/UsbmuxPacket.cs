namespace Netimobiledevice.Usbmuxd;

internal record UsbmuxPacket(UsbmuxdHeader Header, byte[] Payload);

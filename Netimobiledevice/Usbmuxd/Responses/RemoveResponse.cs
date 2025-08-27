using System;

namespace Netimobiledevice.Usbmuxd.Responses;

internal readonly struct RemoveResponse(UsbmuxdHeader header, byte[] data)
{
    public UsbmuxdHeader Header { get; } = header;
    public uint DeviceId { get; } = BitConverter.ToUInt32(data);
}

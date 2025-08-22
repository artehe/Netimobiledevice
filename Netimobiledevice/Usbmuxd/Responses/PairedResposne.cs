using System;

namespace Netimobiledevice.Usbmuxd.Responses;

internal readonly struct PairedResposne(UsbmuxdHeader header, byte[] data)
{
    public UsbmuxdHeader Header { get; } = header;
    public uint DeviceId { get; } = BitConverter.ToUInt32(data);
}

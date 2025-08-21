using System;

namespace Netimobiledevice.Usbmuxd.Responses;

internal readonly struct ResultResponse(UsbmuxdHeader header, byte[] data)
{
    public UsbmuxdHeader Header { get; } = header;
    public UsbmuxdResult Result { get; } = (UsbmuxdResult) BitConverter.ToInt32(data);
}

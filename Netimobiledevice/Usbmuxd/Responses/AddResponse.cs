namespace Netimobiledevice.Usbmuxd.Responses;

internal readonly struct AddResponse(UsbmuxdHeader header, byte[] data)
{
    public UsbmuxdHeader Header { get; } = header;
    public UsbmuxdDeviceRecord DeviceRecord { get; } = UsbmuxdDeviceRecord.FromBytes(data);
}

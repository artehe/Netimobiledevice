namespace Netimobiledevice.Usbmuxd.Responses;

internal readonly struct RemoveResponse
{
    public UsbmuxdHeader Header { get; }
    public int DeviceId { get; }

    public RemoveResponse(UsbmuxdHeader header, byte[] data)
    {
        Header = header;
        DeviceId = BitConverter.ToInt32(data);
    }
}

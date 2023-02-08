namespace Netimobiledevice.Usbmuxd.Responses;

internal readonly struct ResultResponse
{
    public UsbmuxdHeader Header { get; }
    public UsbmuxdResult Result { get; }

    public ResultResponse(UsbmuxdHeader header, byte[] data)
    {
        Header = header;

        int resultInt = BitConverter.ToInt32(data);
        Result = (UsbmuxdResult) resultInt;
    }
}

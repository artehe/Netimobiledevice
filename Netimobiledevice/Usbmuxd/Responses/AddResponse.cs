namespace Netimobiledevice.Usbmuxd.Responses
{
    internal readonly struct AddResponse
    {
        public UsbmuxdHeader Header { get; }
        public UsbmuxdDeviceRecord DeviceRecord { get; }

        public AddResponse(UsbmuxdHeader header, byte[] data)
        {
            Header = header;
            DeviceRecord = UsbmuxdDeviceRecord.FromBytes(data);
        }
    }
}

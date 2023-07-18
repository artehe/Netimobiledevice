using System;

namespace Netimobiledevice.Usbmuxd.Responses
{
    internal readonly struct RemoveResponse
    {
        public UsbmuxdHeader Header { get; }
        public uint DeviceId { get; }

        public RemoveResponse(UsbmuxdHeader header, byte[] data)
        {
            Header = header;
            DeviceId = BitConverter.ToUInt32(data);
        }
    }
}

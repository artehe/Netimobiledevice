using System;

namespace Netimobiledevice.Usbmuxd.Responses
{
    internal readonly struct PairedResposne
    {
        public UsbmuxdHeader Header { get; }
        public uint DeviceId { get; }

        public PairedResposne(UsbmuxdHeader header, byte[] data)
        {
            Header = header;
            DeviceId = BitConverter.ToUInt32(data);
        }
    }
}

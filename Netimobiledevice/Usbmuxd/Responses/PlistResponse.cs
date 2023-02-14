using Netimobiledevice.Plist;
using System.IO;

namespace Netimobiledevice.Usbmuxd.Responses
{
    internal readonly struct PlistResponse
    {
        public UsbmuxdHeader Header { get; }
        public PropertyNode Plist { get; }

        public PlistResponse(UsbmuxdHeader header, byte[] data)
        {
            Header = header;

            PropertyNode plist;
            using (Stream stream = new MemoryStream(data)) {
                plist = PropertyList.Load(stream);
            }
            Plist = plist;
        }
    }
}

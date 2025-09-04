using Netimobiledevice.EndianBitConversion;
using System.Text;

namespace Netimobiledevice.Usbmuxd;

internal struct UsbmuxdDeviceRecord
{
    public uint DeviceId;
    public short ProductId;
    public string SerialNumber;
    public int Location;

    public static UsbmuxdDeviceRecord FromBytes(byte[] bytes)
    {
        int stringLength = EndianBitConverter.LittleEndian.ToInt32(bytes, 6);
        return new UsbmuxdDeviceRecord() {
            DeviceId = EndianBitConverter.LittleEndian.ToUInt32(bytes, 0),
            ProductId = EndianBitConverter.LittleEndian.ToInt16(bytes, 4),
            SerialNumber = Encoding.UTF8.GetString(bytes, 10, stringLength),
            Location = EndianBitConverter.LittleEndian.ToInt32(bytes, 10 + stringLength)
        };
    }
}

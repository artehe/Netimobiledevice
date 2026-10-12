using System.Buffers.Binary;
using System.Linq;
using System.Text;

namespace Netimobiledevice.Usbmuxd;

internal struct UsbmuxdDeviceRecord {
    public uint DeviceId;
    public short ProductId;
    public string SerialNumber;
    public int Location;

    public static UsbmuxdDeviceRecord FromBytes(byte[] bytes) {
        int stringLength = BinaryPrimitives.ReadInt32LittleEndian(bytes.Skip(6).ToArray());
        return new UsbmuxdDeviceRecord() {
            DeviceId = BinaryPrimitives.ReadUInt32LittleEndian(bytes),
            ProductId = BinaryPrimitives.ReadInt16LittleEndian(bytes.Skip(4).ToArray()),
            SerialNumber = Encoding.UTF8.GetString(bytes, 10, stringLength),
            Location = BinaryPrimitives.ReadInt32LittleEndian(bytes.Skip(10 + stringLength).ToArray())
        };
    }
}

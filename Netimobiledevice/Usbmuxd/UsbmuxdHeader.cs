using Netimobiledevice.EndianBitConversion;

namespace Netimobiledevice.Usbmuxd;

internal struct UsbmuxdHeader
{
    /// <summary>
    /// Length of message including header
    /// </summary>
    public int Length;
    /// <summary>
    /// Protocol version
    /// </summary>
    public UsbmuxdVersion Version;
    /// <summary>
    /// Message type
    /// </summary>
    public UsbmuxdMessageType Message;
    /// <summary>
    /// Responses to this query will echo back this tag
    /// </summary>
    public int Tag;

    public readonly byte[] GetBytes()
    {
        return [
            .. EndianBitConverter.LittleEndian.GetBytes(Length),
            .. EndianBitConverter.LittleEndian.GetBytes((uint) Version),
            .. EndianBitConverter.LittleEndian.GetBytes((uint) Message),
            .. EndianBitConverter.LittleEndian.GetBytes(Tag),
        ];
    }

    public static UsbmuxdHeader FromBytes(byte[] bytes)
    {
        return new UsbmuxdHeader() {
            Length = EndianBitConverter.LittleEndian.ToInt32(bytes, 0),
            Version = (UsbmuxdVersion) EndianBitConverter.LittleEndian.ToUInt32(bytes, 4),
            Message = (UsbmuxdMessageType) EndianBitConverter.LittleEndian.ToUInt32(bytes, 8),
            Tag = EndianBitConverter.LittleEndian.ToInt32(bytes, 12),
        };
    }
}

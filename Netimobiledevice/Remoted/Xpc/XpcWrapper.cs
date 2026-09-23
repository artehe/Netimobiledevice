using System;
using System.Linq;

namespace Netimobiledevice.Remoted.Xpc;

public class XpcWrapper {
    public const uint MAGIC = 0x29b00b92;

    public uint Magic { get; private set; }
    public XpcFlags Flags { get; set; }
    public required XpcMessage Message { get; set; }

    public static XpcWrapper Create(XpcDictionary data, ulong messageId = 0, bool wantingReply = false) {
        XpcFlags flags = XpcFlags.AlwaysSet;
        if (data.Count > 0) {
            flags |= XpcFlags.DataPresent;
        }
        if (wantingReply) {
            flags |= XpcFlags.WantingReply;
        }

        return new XpcWrapper() {
            Magic = MAGIC,
            Flags = flags,
            Message = new XpcMessage() {
                MessageId = (uint) messageId,
                Payload = new XpcPayload() {
                    Obj = data
                }
            }
        };
    }

    public byte[] Serialise() {
        return [
            .. BitConverter.GetBytes(Magic),
            .. BitConverter.GetBytes((uint) Flags),
            .. Message.Serialise()
        ];
    }

    public static XpcWrapper Deserialise(byte[] data) {
        uint magic = BitConverter.ToUInt32(data, 0);
        if (magic != MAGIC) {
            throw new DataMisalignedException($"Missing correct magic got {magic} instead of {MAGIC}");
        }
        return new XpcWrapper() {
            Magic = magic,
            Flags = (XpcFlags) BitConverter.ToUInt32(data.Skip(4).Take(4).ToArray()),
            Message = XpcMessage.Deserialise(data.Skip(8).ToArray())
        };
    }
}

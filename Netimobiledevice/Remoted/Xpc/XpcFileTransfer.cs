using System;
using System.Linq;

namespace Netimobiledevice.Remoted.Xpc;

/// <summary>
/// An XPC file-transfer object: the payload itself travels on its own HTTP/2 stream.
/// <see cref="TransferId"/> correlates the announcement with that stream. It is always
/// 0 on objects decoded off the wire, mirroring upstream pymobiledevice3, whose
/// receiving code derives the stream positionally rather than from this field.
/// </summary>
public class XpcFileTransfer(ulong transferSize, ulong transferId = 0) : XpcObject
{
    public ulong TransferSize { get; set; } = transferSize;

    public ulong TransferId { get; set; } = transferId;

    public override bool IsAligned => false;

    public override bool IsPrefixed => false;

    public override XpcMessageType Type => XpcMessageType.FileTransfer;

    public static XpcFileTransfer Deserialise(byte[] data)
    {
        // The leading 8-byte msg_id is intentionally not surfaced here; see TransferId above.
        XpcObject nested = XpcSerialiser.Deserialise(data.Skip(8).ToArray());
        XpcDictionary dict = nested.AsXpcDictionary();
        ulong transferSize = ((XpcUInt64) dict["s"]).Data;
        return new XpcFileTransfer(transferSize);
    }

    public override byte[] Serialise()
    {
        XpcDictionary dict = new() {
            { "s", new XpcUInt64(TransferSize) }
        };
        return [
            .. BitConverter.GetBytes(TransferId),
            .. XpcSerialiser.Serialise(dict)
        ];
    }
}

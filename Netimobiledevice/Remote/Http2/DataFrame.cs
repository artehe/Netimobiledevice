using System;
using System.IO;

namespace Netimobiledevice.Remote.Http2;

internal sealed class DataFrame : Frame {
    private byte _padLength;

    public byte[] Data { get; set; } = [];
    public override FrameType Type => FrameType.Data;

    public byte PadLength {
        get => _padLength;
        set {
            _padLength = value;
            if (value > 0) {
                Flags |= FrameFlags.Padded;
            }
            else {
                Flags &= ~FrameFlags.Padded;
            }
        }
    }

    protected override ReadOnlyMemory<byte> Payload {
        get {
            if (!Flags.HasFlag(FrameFlags.Padded)) {
                return Data;
            }

            byte[] payload = new byte[checked(1 + Data.Length + _padLength)];
            payload[0] = _padLength;

            Data.AsSpan().CopyTo(payload.AsSpan(1));
            return payload;
        }
    }

    public DataFrame() { }

    public DataFrame(uint streamIdentifier) {
        StreamIdentifier = streamIdentifier;
    }

    public override void ParsePayload(
        ReadOnlySpan<byte> payloadData,
        FrameHeader frameHeader
    ) {
        if (!Flags.HasFlag(FrameFlags.Padded)) {
            _padLength = 0;
            Data = payloadData.ToArray();
            return;
        }

        // A padded DATA frame must contain at least the
        // one-byte Pad Length field.
        if (payloadData.Length < 1) {
            throw new InvalidDataException(
                "A padded DATA frame must contain a Pad Length field.");
        }

        byte padLength = payloadData[0];

        // The Pad Length cannot exceed the number of bytes
        // remaining after the Pad Length field.
        if (padLength > payloadData.Length - 1) {
            throw new InvalidDataException(
                $"DATA frame specifies {padLength} bytes of padding, " +
                $"but only {payloadData.Length - 1} bytes are available.");
        }

        _padLength = padLength;

        int dataLength = payloadData.Length - 1 - padLength;

        Data = payloadData
            .Slice(1, dataLength)
            .ToArray();
    }

    public override string ToString() =>
        $"[Frame: DATA, " +
        $"Id={StreamIdentifier}, " +
        $"EndStream={Flags.HasFlag(FrameFlags.EndStream)}, " +
        $"Padded={Flags.HasFlag(FrameFlags.Padded)}, " +
        $"PadLength={PadLength}, " +
        $"DataLength={Data.Length}, " +
        $"PayloadLength={PayloadLength}]";
}

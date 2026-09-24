using System;
using System.Buffers.Binary;
using System.IO;

namespace Netimobiledevice.Remote.Http2;

/// <summary>
/// HTTP/2 PUSH_PROMISE frame (RFC 9113 §6.6).
/// </summary>
internal sealed class PushPromiseFrame : Frame {
    private const uint StreamIdMask = 0x7FFFFFFF;
    private const int PromisedStreamIdLength = 4;

    private byte padLength;
    private uint promisedStreamId;

    public override FrameType Type => FrameType.PushPromise;

    public bool EndHeaders {
        get => HasFlag(FrameFlags.EndHeaders);
        set => SetFlag(FrameFlags.EndHeaders, value);
    }

    /// <summary>
    /// When true, a pad length byte and that many zero bytes of padding are written.
    /// Clearing it also resets <see cref="PadLength"/> to 0.
    /// </summary>
    public bool Padded {
        get => HasFlag(FrameFlags.Padded);
        set {
            SetFlag(FrameFlags.Padded, value);
            if (!value) {
                padLength = 0;
            }
        }
    }

    /// <summary>
    /// Number of padding bytes (0-255). Setting this also sets the PADDED flag,
    /// because a pad length is only meaningful (and only sent) when PADDED is set.
    /// </summary>
    public byte PadLength {
        get => padLength;
        set {
            padLength = value;
            SetFlag(FrameFlags.Padded, true);
        }
    }

    /// <summary>
    /// The 31-bit identifier of the stream being reserved by this promise.
    /// </summary>
    public uint PromisedStreamId {
        get => promisedStreamId;
        set {
            if (value > StreamIdMask) {
                throw new ArgumentOutOfRangeException(nameof(value), "Promised stream identifier must be a 31-bit unsigned integer.");
            }
            promisedStreamId = value;
        }
    }

    /// <summary>
    /// The (possibly partial) HPACK-encoded header block. Never null.
    /// </summary>
    public ReadOnlyMemory<byte> HeaderBlockFragment { get; set; } = ReadOnlyMemory<byte>.Empty;

    protected override ReadOnlyMemory<byte> Payload => BuildPayload();

    public PushPromiseFrame() {
    }

    public PushPromiseFrame(uint streamIdentifier) {
        StreamIdentifier = streamIdentifier;
    }

    public PushPromiseFrame(uint streamIdentifier, uint promisedStreamId) {
        StreamIdentifier = streamIdentifier;
        PromisedStreamId = promisedStreamId;
    }

    private bool HasFlag(FrameFlags flag) => (Flags & flag) != 0;

    private void SetFlag(FrameFlags flag, bool value) {
        Flags = value ? Flags | flag : Flags & ~flag;
    }

    private byte[] BuildPayload() {
        bool padded = Padded;
        ReadOnlySpan<byte> fragment = HeaderBlockFragment.Span;

        int length = (padded ? 1 : 0)
            + PromisedStreamIdLength
            + fragment.Length
            + (padded ? padLength : 0);

        // Zero-initialised, so the trailing padding is already correct.
        byte[] buffer = new byte[length];
        Span<byte> span = buffer;
        int offset = 0;

        if (padded) {
            span[offset++] = padLength;
        }

        // The reserved (R) bit is always sent as 0.
        BinaryPrimitives.WriteUInt32BigEndian(span.Slice(offset, PromisedStreamIdLength), promisedStreamId & StreamIdMask);
        offset += PromisedStreamIdLength;

        fragment.CopyTo(span.Slice(offset));

        return buffer;
    }

    public override void ParsePayload(ReadOnlySpan<byte> payloadData, FrameHeader frameHeader) {
        // Flags and StreamIdentifier were already assigned by Frame.Parse.
        if (StreamIdentifier == 0) {
            throw new InvalidDataException("HTTP/2 PUSH_PROMISE frame must be associated with a stream (stream identifier was 0).");
        }

        int offset = 0;

        if (Padded) {
            if (payloadData.Length < 1) {
                throw new InvalidDataException("HTTP/2 PUSH_PROMISE frame is PADDED but has no pad length field.");
            }
            padLength = payloadData[offset++];
        }
        else {
            padLength = 0;
        }

        if (payloadData.Length - offset < PromisedStreamIdLength) {
            throw new InvalidDataException("HTTP/2 PUSH_PROMISE frame is too short to contain a promised stream identifier.");
        }

        // Mask off the reserved bit.
        promisedStreamId = BinaryPrimitives.ReadUInt32BigEndian(payloadData.Slice(offset, PromisedStreamIdLength)) & StreamIdMask;
        offset += PromisedStreamIdLength;

        if (promisedStreamId == 0) {
            throw new InvalidDataException("HTTP/2 PUSH_PROMISE frame has an invalid promised stream identifier of 0.");
        }

        int fragmentLength = payloadData.Length - offset - padLength;
        if (fragmentLength < 0) {
            throw new InvalidDataException("HTTP/2 PUSH_PROMISE frame pad length exceeds the remaining payload.");
        }

        // The span is only valid for the duration of this call, so the fragment must be copied.
        HeaderBlockFragment = payloadData.Slice(offset, fragmentLength).ToArray();

        // Padding bytes are intentionally ignored.
    }

    public override string ToString() => $"[Frame: PUSH_PROMISE, Id={StreamIdentifier}, PromisedStreamId={PromisedStreamId}, EndHeaders={EndHeaders}, " +
        $"Padded={Padded}, PadLength={PadLength}, HeaderBlockFragmentLength={HeaderBlockFragment.Length}]";
}

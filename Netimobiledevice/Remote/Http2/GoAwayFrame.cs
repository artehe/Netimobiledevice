using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace Netimobiledevice.Remote.Http2;

/// <summary>
/// HTTP/2 GOAWAY frame (RFC 9113 §6.8).
/// </summary>
internal sealed class GoAwayFrame : Frame {
    private const uint StreamIdMask = 0x7FFFFFFF;
    private const int FixedPayloadLength = 8;

    private uint lastStreamId;

    public override FrameType Type => FrameType.GoAway;

    /// <summary>
    /// GOAWAY defines no flags: they are always sent as 0 and ignored on receipt.
    /// </summary>
    public override FrameFlags Flags {
        get => default;
        internal set { }
    }

    /// <summary>
    /// The highest-numbered stream the sender might have acted on (31-bit).
    /// </summary>
    public uint LastStreamId {
        get => lastStreamId;
        set {
            if (value > StreamIdMask) {
                throw new ArgumentOutOfRangeException(nameof(value), "Last stream identifier must be a 31-bit unsigned integer.");
            }
            lastStreamId = value;
        }
    }

    public ErrorCode ErrorCode { get; set; }

    /// <summary>
    /// Optional opaque diagnostic data. Never null.
    /// </summary>
    public ReadOnlyMemory<byte> AdditionalDebugData { get; set; } = ReadOnlyMemory<byte>.Empty;

    protected override ReadOnlyMemory<byte> Payload {
        get {
            ReadOnlySpan<byte> debugData = AdditionalDebugData.Span;

            byte[] buffer = new byte[FixedPayloadLength + debugData.Length];

            // The reserved (R) bit is always sent as 0.
            BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(0, 4), lastStreamId & StreamIdMask);
            BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(4, 4), (uint) ErrorCode);
            debugData.CopyTo(buffer.AsSpan(FixedPayloadLength));

            return buffer;
        }
    }

    public GoAwayFrame() {
    }

    public GoAwayFrame(uint lastStreamId, ErrorCode errorCode) {
        LastStreamId = lastStreamId;
        ErrorCode = errorCode;
    }

    public override void ParsePayload(ReadOnlySpan<byte> payloadData, FrameHeader frameHeader) {
        // Flags and StreamIdentifier were already assigned by Frame.Parse.
        if (StreamIdentifier != 0) {
            throw new InvalidDataException($"HTTP/2 GOAWAY frame must use stream 0 but used stream {StreamIdentifier}.");
        }

        if (payloadData.Length < FixedPayloadLength) {
            throw new InvalidDataException($"HTTP/2 GOAWAY frame payload must be at least {FixedPayloadLength} bytes but was {payloadData.Length}.");
        }

        // Mask off the reserved bit.
        lastStreamId = BinaryPrimitives.ReadUInt32BigEndian(payloadData.Slice(0, 4)) & StreamIdMask;
        ErrorCode = (ErrorCode) BinaryPrimitives.ReadUInt32BigEndian(payloadData.Slice(4, 4));

        // Always assign, so a reused instance doesn't keep stale debug data.
        AdditionalDebugData = payloadData.Length > FixedPayloadLength
            ? payloadData.Slice(FixedPayloadLength).ToArray()
            : ReadOnlyMemory<byte>.Empty;
    }

    public override string ToString() {
        // The debug data is opaque, but in practice it is a UTF-8 diagnostic string.
        string debug = AdditionalDebugData.IsEmpty ? string.Empty : Encoding.UTF8.GetString(AdditionalDebugData.Span);

        return $"[Frame: GOAWAY, Id={StreamIdentifier}, ErrorCode={ErrorCode}, LastStreamId={LastStreamId}, AdditionalDebugData={debug}]";
    }
}

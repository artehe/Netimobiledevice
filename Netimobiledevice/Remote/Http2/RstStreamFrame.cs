using System;
using System.Buffers.Binary;
using System.IO;

namespace Netimobiledevice.Remote.Http2;

/// <summary>
/// HTTP/2 RST_STREAM frame (RFC 9113 §6.4).
/// </summary>
internal sealed class RstStreamFrame : Frame {
    private const int PayloadSize = 4;

    public ErrorCode ErrorCode { get; set; }

    public override FrameType Type => FrameType.RstStream;

    /// <summary>
    /// RST_STREAM defines no flags: they are always sent as 0 and ignored on receipt.
    /// </summary>
    public override FrameFlags Flags {
        get => default;
        internal set { }
    }

    protected override ReadOnlyMemory<byte> Payload {
        get {
            byte[] buffer = new byte[PayloadSize];
            BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint) ErrorCode);
            return buffer;
        }
    }

    public RstStreamFrame() {
    }

    public RstStreamFrame(uint streamIdentifier) {
        StreamIdentifier = streamIdentifier;
    }

    public RstStreamFrame(uint streamIdentifier, ErrorCode errorCode) {
        StreamIdentifier = streamIdentifier;
        ErrorCode = errorCode;
    }

    public override void ParsePayload(ReadOnlySpan<byte> payloadData, FrameHeader frameHeader) {
        // Flags and StreamIdentifier were already assigned by Frame.Parse.
        if (StreamIdentifier == 0) {
            throw new InvalidDataException("HTTP/2 RST_STREAM frame must be associated with a stream (stream identifier was 0).");
        }

        if (payloadData.Length != PayloadSize) {
            throw new InvalidDataException($"HTTP/2 RST_STREAM frame payload must be exactly {PayloadSize} bytes but was {payloadData.Length}.");
        }

        // Unknown error codes are preserved as-is; the caller decides how to treat them
        // (the RFC says they may be treated as INTERNAL_ERROR).
        ErrorCode = (ErrorCode) BinaryPrimitives.ReadUInt32BigEndian(payloadData);
    }

    public override string ToString() => $"[Frame: RST_STREAM, Id={StreamIdentifier}, ErrorCode={ErrorCode}]";
}

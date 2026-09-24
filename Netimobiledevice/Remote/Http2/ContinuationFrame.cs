using System;
using System.IO;

namespace Netimobiledevice.Remote.Http2;

/// <summary>
/// HTTP/2 CONTINUATION frame (RFC 9113 §6.10).
/// </summary>
internal sealed class ContinuationFrame : Frame {
    public override FrameType Type => FrameType.Continuation;

    public bool EndHeaders {
        get => (Flags & FrameFlags.EndHeaders) != 0;
        set => Flags = value ? Flags | FrameFlags.EndHeaders : Flags & ~FrameFlags.EndHeaders;
    }

    /// <summary>
    /// The (possibly partial) HPACK-encoded header block. Never null.
    /// </summary>
    public ReadOnlyMemory<byte> HeaderBlockFragment { get; set; } = ReadOnlyMemory<byte>.Empty;

    protected override ReadOnlyMemory<byte> Payload => HeaderBlockFragment;

    public ContinuationFrame() {
    }

    public ContinuationFrame(uint streamIdentifier) {
        StreamIdentifier = streamIdentifier;
    }

    public override void ParsePayload(ReadOnlySpan<byte> payloadData, FrameHeader frameHeader) {
        // Flags and StreamIdentifier were already assigned by Frame.Parse.
        if (StreamIdentifier == 0) {
            throw new InvalidDataException("HTTP/2 CONTINUATION frame must be associated with a stream (stream identifier was 0).");
        }

        // The span is only valid for the duration of this call, so the fragment must be copied.
        HeaderBlockFragment = payloadData.ToArray();
    }

    public override string ToString() => $"[Frame: CONTINUATION, Id={StreamIdentifier}, EndHeaders={EndHeaders}, HeaderBlockFragmentLength={HeaderBlockFragment.Length}]";
}

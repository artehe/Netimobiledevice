using System;
using System.Buffers.Binary;
using System.IO;

namespace Netimobiledevice.Remote.Http2;

/// <summary>
/// HTTP/2 WINDOW_UPDATE frame (RFC 9113 §6.9).
/// </summary>
internal sealed class WindowUpdateFrame : Frame {
    private const uint MaxIncrement = 0x7FFFFFFF;
    private const int PayloadSize = 4;

    private uint windowSizeIncrement;

    public override FrameType Type => FrameType.WindowUpdate;

    /// <summary>
    /// WINDOW_UPDATE defines no flags: they are always sent as 0 and ignored on receipt.
    /// </summary>
    public override FrameFlags Flags {
        get => default;
        internal set { }
    }

    /// <summary>
    /// The number of octets to add to the flow-control window (1 to 2^31-1).
    /// </summary>
    public uint WindowSizeIncrement {
        get => windowSizeIncrement;
        set {
            if (value is 0 or > MaxIncrement) {
                throw new ArgumentOutOfRangeException(nameof(value), $"Window size increment must be between 1 and {MaxIncrement:N0}.");
            }
            windowSizeIncrement = value;
        }
    }

    protected override ReadOnlyMemory<byte> Payload {
        get {
            if (windowSizeIncrement == 0) {
                throw new InvalidOperationException("HTTP/2 WINDOW_UPDATE window size increment must be set to a value between 1 and 2^31-1.");
            }

            byte[] buffer = new byte[PayloadSize];

            // The reserved (R) bit is always sent as 0.
            BinaryPrimitives.WriteUInt32BigEndian(buffer, windowSizeIncrement & MaxIncrement);

            return buffer;
        }
    }

    public WindowUpdateFrame() {
    }

    public WindowUpdateFrame(uint streamIdentifier) {
        StreamIdentifier = streamIdentifier;
    }

    public WindowUpdateFrame(uint streamIdentifier, uint windowSizeIncrement) {
        StreamIdentifier = streamIdentifier;
        WindowSizeIncrement = windowSizeIncrement;
    }

    public override void ParsePayload(ReadOnlySpan<byte> payloadData, FrameHeader frameHeader) {
        // Flags and StreamIdentifier were already assigned by Frame.Parse.
        if (payloadData.Length != PayloadSize) {
            throw new InvalidDataException($"HTTP/2 WINDOW_UPDATE frame payload must be exactly {PayloadSize} bytes but was {payloadData.Length}.");
        }

        // Mask off the reserved bit.
        uint increment = BinaryPrimitives.ReadUInt32BigEndian(payloadData) & MaxIncrement;

        if (increment == 0) {
            throw new InvalidDataException("HTTP/2 WINDOW_UPDATE frame has a window size increment of 0.");
        }

        windowSizeIncrement = increment;
    }

    public override string ToString() => $"[Frame: WINDOW_UPDATE, Id={StreamIdentifier}, WindowSizeIncrement={WindowSizeIncrement}]";
}

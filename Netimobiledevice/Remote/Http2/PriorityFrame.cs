using System;
using System.Buffers.Binary;
using System.IO;

namespace Netimobiledevice.Remote.Http2;

/// <summary>
/// HTTP/2 PRIORITY frame (RFC 9113 §6.3). 
/// </summary>
internal sealed class PriorityFrame : Frame {
    private const uint ExclusiveBit = 0x80000000;
    private const uint StreamIdMask = 0x7FFFFFFF;
    private const int PayloadSize = 5;

    private uint streamDependency;

    public override FrameType Type => FrameType.Priority;

    /// <summary>
    /// PRIORITY defines no flags: they are always sent as 0 and ignored on receipt.
    /// </summary>
    public override FrameFlags Flags {
        get => default;
        internal set { }
    }

    /// <summary>
    /// The E bit: makes this stream the sole dependent of <see cref="StreamDependency"/>.
    /// </summary>
    public bool Exclusive { get; set; }

    /// <summary>
    /// The 31-bit stream this stream depends on.
    /// </summary>
    public uint StreamDependency {
        get => streamDependency;
        set {
            if (value > StreamIdMask) {
                throw new ArgumentOutOfRangeException(nameof(value), "Stream dependency must be a 31-bit unsigned integer.");
            }
            streamDependency = value;
        }
    }

    /// <summary>
    /// Wire value of the weight byte (0-255). The effective HTTP/2 weight is this value + 1.
    /// </summary>
    public byte Weight { get; set; }

    protected override ReadOnlyMemory<byte> Payload {
        get {
            byte[] buffer = new byte[PayloadSize];

            uint dependency = streamDependency & StreamIdMask;
            if (Exclusive) {
                dependency |= ExclusiveBit;
            }

            BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(0, 4), dependency);
            buffer[4] = Weight;

            return buffer;
        }
    }

    public PriorityFrame() {
    }

    public PriorityFrame(uint streamIdentifier) {
        StreamIdentifier = streamIdentifier;
    }

    public override void ParsePayload(ReadOnlySpan<byte> payloadData, FrameHeader frameHeader) {
        // Flags and StreamIdentifier were already assigned by Frame.Parse.
        if (StreamIdentifier == 0) {
            throw new InvalidDataException("HTTP/2 PRIORITY frame must be associated with a stream (stream identifier was 0).");
        }

        if (payloadData.Length != PayloadSize) {
            throw new InvalidDataException($"HTTP/2 PRIORITY frame payload must be exactly {PayloadSize} bytes but was {payloadData.Length}.");
        }

        uint dependency = BinaryPrimitives.ReadUInt32BigEndian(payloadData.Slice(0, 4));
        Exclusive = (dependency & ExclusiveBit) != 0;
        streamDependency = dependency & StreamIdMask;
        Weight = payloadData[4];

        if (streamDependency == StreamIdentifier) {
            throw new InvalidDataException($"HTTP/2 PRIORITY frame: stream {StreamIdentifier} cannot depend on itself.");
        }
    }

    public override string ToString() => $"[Frame: PRIORITY, Id={StreamIdentifier}, Exclusive={Exclusive}, StreamDependency={StreamDependency}, Weight={Weight}]";
}

using Netimobiledevice.Remote.Http2;
using System;
using System.Buffers.Binary;
using System.IO;

namespace Netimobiledevice.Remoted.Frames;

/// <summary>
/// HTTP/2 HEADERS frameFC 9113 §6.2).
/// </summary>
internal sealed class HeadersFrame : Frame {
    private const uint ExclusiveBit = 0x80000000;
    private const uint StreamIdMask = 0x7FFFFFFF;
    private const int PriorityFieldLength = 5; // 4 bytes (E + stream dependency) + 1 byte weight

    private byte padLength;

    public override FrameType Type => FrameType.Headers;

    public bool EndStream {
        get => HasFlag(FrameFlags.EndStream);
        set => SetFlag(FrameFlags.EndStream, value);
    }

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
    /// When true, the exclusive bit, stream dependency and weight fields are present.
    /// </summary>
    public bool Priority {
        get => HasFlag(FrameFlags.Priority);
        set => SetFlag(FrameFlags.Priority, value);
    }

    /// <summary>
    /// The E bit of the priority fields.
    /// </summary>
    public bool Exclusive { get; set; }

    private uint streamDependency;

    /// <summary>
    /// The 31-bit stream this stream depends on. Only sent when <see cref="Priority"/> is set.
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
    /// Only sent when <see cref="Priority"/> is set.
    /// </summary>
    public byte Weight { get; set; }

    /// <summary>
    /// The (possibly partial) HPACK-encoded header block. Never null.
    /// </summary>
    public ReadOnlyMemory<byte> HeaderBlockFragment { get; set; } = ReadOnlyMemory<byte>.Empty;

    protected override ReadOnlyMemory<byte> Payload => BuildPayload();

    public HeadersFrame() { }

    public HeadersFrame(uint streamIdentifier) {
        StreamIdentifier = streamIdentifier;
    }

    private bool HasFlag(FrameFlags flag) => (Flags & flag) != 0;

    private void SetFlag(FrameFlags flag, bool value) {
        Flags = value ? Flags | flag : Flags & ~flag;
    }

    private byte[] BuildPayload() {
        bool padded = Padded;
        bool priority = Priority;
        ReadOnlySpan<byte> fragment = HeaderBlockFragment.Span;

        int length = (padded ? 1 : 0)
            + (priority ? PriorityFieldLength : 0)
            + fragment.Length
            + (padded ? padLength : 0);

        // Zero-initialised, so the trailing padding is already correct.
        byte[] buffer = new byte[length];
        Span<byte> span = buffer;
        int offset = 0;

        if (padded) {
            span[offset++] = padLength;
        }

        if (priority) {
            uint dependency = streamDependency & StreamIdMask;
            if (Exclusive) {
                dependency |= ExclusiveBit;
            }
            BinaryPrimitives.WriteUInt32BigEndian(span.Slice(offset, 4), dependency);
            offset += 4;
            span[offset++] = Weight;
        }

        fragment.CopyTo(span.Slice(offset));

        return buffer;
    }


    public override void ParsePayload(ReadOnlySpan<byte> payloadData, FrameHeader frameHeader) {
        // Flags and StreamIdentifier were already assigned by Frame.Parse.
        if (StreamIdentifier == 0) {
            throw new InvalidDataException("HTTP/2 HEADERS frame must be associated with a stream (stream identifier was 0).");
        }

        int offset = 0;

        if (Padded) {
            if (payloadData.Length < 1) {
                throw new InvalidDataException("HTTP/2 HEADERS frame is PADDED but has no pad length field.");
            }
            padLength = payloadData[offset++];
        }
        else {
            padLength = 0;
        }

        if (Priority) {
            if (payloadData.Length - offset < PriorityFieldLength) {
                throw new InvalidDataException("HTTP/2 HEADERS frame has the PRIORITY flag set but is too short to contain priority fields.");
            }

            uint dependency = BinaryPrimitives.ReadUInt32BigEndian(payloadData.Slice(offset, 4));
            Exclusive = (dependency & ExclusiveBit) != 0;
            streamDependency = dependency & StreamIdMask;
            Weight = payloadData[offset + 4];
            offset += PriorityFieldLength;
        }
        else {
            Exclusive = false;
            streamDependency = 0;
            Weight = 0;
        }

        int fragmentLength = payloadData.Length - offset - padLength;
        if (fragmentLength < 0) {
            throw new InvalidDataException("HTTP/2 HEADERS frame pad length exceeds the remaining payload.");
        }

        // The span is only valid for the duration of this call, so the fragment must be copied.
        HeaderBlockFragment = payloadData.Slice(offset, fragmentLength).ToArray();

        // Padding bytes are intentionally ignored.
    }

    public override string ToString() => $"[Frame: HEADERS, Id={StreamIdentifier}, EndStream={EndStream}, EndHeaders={EndHeaders}, " +
        $"Priority={Priority}, Exclusive={Exclusive}, StreamDependency={StreamDependency}, Weight={Weight}, " +
        $"Padded={Padded}, PadLength={PadLength}, HeaderBlockFragmentLength={HeaderBlockFragment.Length}]";
}

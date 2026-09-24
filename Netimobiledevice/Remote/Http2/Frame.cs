using Netimobiledevice.Remoted.Frames;
using System;
using System.Buffers.Binary;
using System.IO;

namespace Netimobiledevice.Remote.Http2;

internal abstract class Frame {
    private const uint MaxPayloadLength = 0xFFFFFF;
    private const uint StreamIdMask = 0x7FFFFFFF;

    public const int HeaderLength = 9;

    protected abstract ReadOnlyMemory<byte> Payload { get; }

    public virtual FrameFlags Flags { get; internal set; }
    public uint StreamIdentifier { get; set; }
    public abstract FrameType Type { get; }

    public bool IsEndStream => Type is FrameType.Data or FrameType.Headers && (Flags & FrameFlags.EndStream) != 0;
    public uint PayloadLength => checked((uint) Payload.Length);

    /// <summary> 
    /// Creates a frame instance for the specified frame type. 
    /// </summary> 
    public static Frame Create(FrameType frameType) {
        return frameType switch {
            FrameType.Data => new DataFrame(),
            FrameType.Headers => new HeadersFrame(),
            FrameType.Priority => new PriorityFrame(),
            FrameType.RstStream => new RstStreamFrame(),
            FrameType.Settings => new SettingsFrame(),
            FrameType.PushPromise => new PushPromiseFrame(),
            FrameType.Ping => new PingFrame(),
            FrameType.GoAway => new GoAwayFrame(),
            FrameType.WindowUpdate => new WindowUpdateFrame(),
            FrameType.Continuation => new ContinuationFrame(),
            _ => throw new NetimobiledeviceException($"Unknown HTTP/2 frame type: 0x{(byte) frameType:X2}")
        };
    }

    public static Frame Create(byte frameType) => Create((FrameType) frameType);

    public void Parse(ReadOnlySpan<byte> data) {
        FrameHeader header = ParseFrameHeader(data);
        if (header.Length > data.Length - HeaderLength) {
            throw new InvalidDataException("HTTP/2 frame payload is incomplete.");
        }

        StreamIdentifier = header.StreamIdentifier;
        Flags = header.Flags;

        ReadOnlySpan<byte> payload = data.Slice(HeaderLength, checked((int) header.Length));
        ParsePayload(payload, header);
    }

    public static FrameHeader ParseFrameHeader(ReadOnlySpan<byte> data) {
        if (data.Length < HeaderLength) {
            throw new InvalidDataException("HTTP/2 frame is missing its 9-byte header.");
        }

        // HTTP/2 uses a 24-bit unsigned payload length.
        uint frameLength = ((uint) data[0] << 16) | ((uint) data[1] << 8) | data[2];

        // The frame length is the payload length, not the total 
        // frame length. Therefore a zero-length payload is valid.
        if (frameLength > data.Length - HeaderLength) {
            throw new InvalidDataException($"HTTP/2 frame declares a {frameLength:N0}-byte payload, " + $"but only {data.Length - HeaderLength:N0} bytes are available.");
        }

        FrameType frameType = (FrameType) data[3];
        FrameFlags frameFlags = (FrameFlags) data[4];

        // The most significant bit is reserved.
        uint streamIdentifier = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(5, 4)) & StreamIdMask;

        return new FrameHeader(frameLength, frameType, frameFlags, streamIdentifier);
    }

    public abstract void ParsePayload(ReadOnlySpan<byte> payloadData, FrameHeader frameHeader);

    public byte[] ToBytes() {
        ReadOnlySpan<byte> payload = Payload.Span;

        if ((uint) payload.Length > MaxPayloadLength) {
            throw new InvalidOperationException("HTTP/2 payload exceeds the 24-bit length field.");
        }

        if (StreamIdentifier > StreamIdMask) {
            throw new InvalidOperationException($"HTTP/2 stream identifier must be a 31-bit unsigned integer.");
        }

        byte[] result = new byte[HeaderLength + payload.Length];
        result[0] = (byte) (payload.Length >> 16);
        result[1] = (byte) (payload.Length >> 8);
        result[2] = (byte) payload.Length;
        result[3] = (byte) Type;
        result[4] = (byte) Flags;

        BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(5, 4), StreamIdentifier & StreamIdMask);

        payload.CopyTo(result.AsSpan(HeaderLength));

        return result;
    }

    public override string ToString() => $"[Frame: {Type.ToString().ToUpperInvariant()}, " + $"Id={StreamIdentifier}, " + $"Flags={Flags}, " + $"PayloadLength={PayloadLength}, " + $"IsEndStream={IsEndStream}]";
}

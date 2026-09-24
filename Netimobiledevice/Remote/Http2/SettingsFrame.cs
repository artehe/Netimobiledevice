using System;
using System.Buffers.Binary;
using System.IO;

namespace Netimobiledevice.Remote.Http2;

/// <summary>
/// HTTP/2 SETTINGS frame (RFC 9113 §6.5).
/// </summary>
internal sealed class SettingsFrame : Frame {
    private const ushort HeaderTableSizeId = 0x1;
    private const ushort EnablePushId = 0x2;
    private const ushort MaxConcurrentStreamsId = 0x3;
    private const ushort InitialWindowSizeId = 0x4;
    private const ushort MaxFrameSizeId = 0x5;
    private const ushort MaxHeaderListSizeId = 0x6;

    private const int SettingLength = 6;

    private const uint MaxWindowSize = 0x7FFFFFFF;
    private const uint MinFrameSize = 16_384;
    private const uint MaxAllowedFrameSize = 16_777_215;

    public override FrameType Type => FrameType.Settings;

    /// <summary>
    /// When set, this frame acknowledges the peer's SETTINGS and carries no payload
    /// (any settings values on this instance are ignored when serialising).
    /// </summary>
    public bool Ack {
        get => (Flags & FrameFlags.EndStream) != 0;
        set => Flags = value ? Flags | FrameFlags.EndStream : Flags & ~FrameFlags.EndStream;
    }

    public uint? HeaderTableSize { get; set; }        // 0x1, default 4096
    public bool? EnablePush { get; set; }             // 0x2, 0 or 1
    public uint? MaxConcurrentStreams { get; set; }   // 0x3
    public uint? InitialWindowSize { get; set; }      // 0x4, default 65535
    public uint? MaxFrameSize { get; set; }           // 0x5, default 16384
    public uint? MaxHeaderListSize { get; set; }      // 0x6

    protected override ReadOnlyMemory<byte> Payload => Ack ? ReadOnlyMemory<byte>.Empty : BuildPayload();

    public SettingsFrame() {
    }

    /// <summary>
    /// Creates the empty SETTINGS frame that acknowledges the peer's settings.
    /// </summary>
    public static SettingsFrame CreateAck() => new() { Ack = true };

    private byte[] BuildPayload() {
        int count = 0;
        count += HeaderTableSize.HasValue ? 1 : 0;
        count += EnablePush.HasValue ? 1 : 0;
        count += MaxConcurrentStreams.HasValue ? 1 : 0;
        count += InitialWindowSize.HasValue ? 1 : 0;
        count += MaxFrameSize.HasValue ? 1 : 0;
        count += MaxHeaderListSize.HasValue ? 1 : 0;

        byte[] buffer = new byte[count * SettingLength];
        Span<byte> span = buffer;
        int offset = 0;

        if (HeaderTableSize.HasValue) {
            WriteSetting(span, ref offset, HeaderTableSizeId, HeaderTableSize.Value);
        }
        if (EnablePush.HasValue) {
            WriteSetting(span, ref offset, EnablePushId, EnablePush.Value ? 1u : 0u);
        }
        if (MaxConcurrentStreams.HasValue) {
            WriteSetting(span, ref offset, MaxConcurrentStreamsId, MaxConcurrentStreams.Value);
        }
        if (InitialWindowSize.HasValue) {
            WriteSetting(span, ref offset, InitialWindowSizeId, InitialWindowSize.Value);
        }
        if (MaxFrameSize.HasValue) {
            WriteSetting(span, ref offset, MaxFrameSizeId, MaxFrameSize.Value);
        }
        if (MaxHeaderListSize.HasValue) {
            WriteSetting(span, ref offset, MaxHeaderListSizeId, MaxHeaderListSize.Value);
        }

        return buffer;
    }

    private static void WriteSetting(Span<byte> destination, ref int offset, ushort identifier, uint value) {
        BinaryPrimitives.WriteUInt16BigEndian(destination.Slice(offset, 2), identifier);
        BinaryPrimitives.WriteUInt32BigEndian(destination.Slice(offset + 2, 4), value);
        offset += SettingLength;
    }

    public override void ParsePayload(ReadOnlySpan<byte> payloadData, FrameHeader frameHeader) {
        // Flags and StreamIdentifier were already assigned by Frame.Parse.
        if (StreamIdentifier != 0) {
            throw new InvalidDataException($"HTTP/2 SETTINGS frame must use stream 0 but used stream {StreamIdentifier}.");
        }

        if (Ack && payloadData.Length != 0) {
            throw new InvalidDataException("HTTP/2 SETTINGS acknowledgement must have an empty payload.");
        }

        if (payloadData.Length % SettingLength != 0) {
            throw new InvalidDataException($"HTTP/2 SETTINGS payload length ({payloadData.Length}) is not a multiple of {SettingLength}.");
        }

        // Start clean so a reused instance doesn't keep stale values.
        HeaderTableSize = null;
        EnablePush = null;
        MaxConcurrentStreams = null;
        InitialWindowSize = null;
        MaxFrameSize = null;
        MaxHeaderListSize = null;

        for (int i = 0; i < payloadData.Length; i += SettingLength) {
            ushort identifier = BinaryPrimitives.ReadUInt16BigEndian(payloadData.Slice(i, 2));
            uint value = BinaryPrimitives.ReadUInt32BigEndian(payloadData.Slice(i + 2, 4));

            // If a setting appears more than once the last value wins, which plain assignment gives us.
            switch (identifier) {
                case HeaderTableSizeId:
                    HeaderTableSize = value;
                    break;
                case EnablePushId:
                    if (value > 1) {
                        throw new InvalidDataException($"HTTP/2 SETTINGS_ENABLE_PUSH must be 0 or 1 but was {value}.");
                    }
                    EnablePush = value == 1;
                    break;
                case MaxConcurrentStreamsId:
                    MaxConcurrentStreams = value;
                    break;
                case InitialWindowSizeId:
                    if (value > MaxWindowSize) {
                        throw new InvalidDataException($"HTTP/2 SETTINGS_INITIAL_WINDOW_SIZE {value:N0} exceeds {MaxWindowSize:N0}.");
                    }
                    InitialWindowSize = value;
                    break;
                case MaxFrameSizeId:
                    if (value < MinFrameSize || value > MaxAllowedFrameSize) {
                        throw new InvalidDataException($"HTTP/2 SETTINGS_MAX_FRAME_SIZE must be between {MinFrameSize:N0} and {MaxAllowedFrameSize:N0} but was {value:N0}.");
                    }
                    MaxFrameSize = value;
                    break;
                case MaxHeaderListSizeId:
                    MaxHeaderListSize = value;
                    break;
                default:
                    // Unknown settings must be ignored (RFC 9113 §6.5.2).
                    break;
            }
        }
    }

    public override string ToString() => $"[Frame: SETTINGS, Id={StreamIdentifier}, Ack={Ack}, HeaderTableSize={HeaderTableSize}, EnablePush={EnablePush}, " +
        $"MaxConcurrentStreams={MaxConcurrentStreams}, InitialWindowSize={InitialWindowSize}, MaxFrameSize={MaxFrameSize}, MaxHeaderListSize={MaxHeaderListSize}]";
}

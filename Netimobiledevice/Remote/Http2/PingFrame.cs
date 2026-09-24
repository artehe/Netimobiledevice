using System;
using System.IO;

namespace Netimobiledevice.Remote.Http2;

/// <summary>
/// HTTP/2 PING frame (RFC 9113 §6.7).
/// </summary>
internal sealed class PingFrame : Frame {
    /// <summary>
    /// Length of the opaque data in bytes (64 bits).
    /// </summary>
    public const int OpaqueDataLength = 8;

    private byte[] opaqueData = new byte[OpaqueDataLength];

    public override FrameType Type => FrameType.Ping;

    /// <summary>
    /// When set, this frame is the response to a PING and must echo the opaque data of the request.
    /// </summary>
    public bool Ack {
        get => (Flags & FrameFlags.EndStream) != 0;
        set => Flags = value ? Flags | FrameFlags.EndStream : Flags & ~FrameFlags.EndStream;
    }

    /// <summary>
    /// The 8 bytes of opaque data. Assigning copies the value, so later changes to the source array have no effect.
    /// </summary>
    public byte[] OpaqueData {
        get => opaqueData;
        set {
            ArgumentNullException.ThrowIfNull(value);
            if (value.Length != OpaqueDataLength) {
                throw new ArgumentOutOfRangeException(nameof(value), $"Must be exactly {OpaqueDataLength} bytes of data.");
            }
            opaqueData = (byte[]) value.Clone();
        }
    }

    protected override ReadOnlyMemory<byte> Payload => opaqueData;

    public PingFrame() {
    }

    public PingFrame(byte[] opaqueData) {
        OpaqueData = opaqueData;
    }

    /// <summary>
    /// Creates the acknowledgement for this PING, echoing its opaque data as the RFC requires.
    /// </summary>
    public PingFrame CreateAck() => new(opaqueData) { Ack = true };

    public override void ParsePayload(ReadOnlySpan<byte> payloadData, FrameHeader frameHeader) {
        // Flags and StreamIdentifier were already assigned by Frame.Parse.
        if (StreamIdentifier != 0) {
            throw new InvalidDataException($"HTTP/2 PING frame must use stream 0 but used stream {StreamIdentifier}.");
        }

        if (payloadData.Length != OpaqueDataLength) {
            throw new InvalidDataException($"HTTP/2 PING frame payload must be exactly {OpaqueDataLength} bytes but was {payloadData.Length}.");
        }

        opaqueData = payloadData.ToArray();
    }

    public override string ToString() => $"[Frame: PING, Id={StreamIdentifier}, Ack={Ack}, OpaqueData={Convert.ToHexString(opaqueData)}]";
}

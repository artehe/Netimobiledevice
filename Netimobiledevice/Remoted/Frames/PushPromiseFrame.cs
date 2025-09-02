using Netimobiledevice.Extentions;
using System;
using System.Collections.Generic;

namespace Netimobiledevice.Remoted.Frames;

internal class PushPromiseFrame : Frame, IFrameContainsHeaders
{
    private ushort _padLength = 0;

    public bool EndHeaders { get; set; }
    public byte[] HeaderBlockFragment { get; set; }
    public bool Padded { get; set; }

    public override byte Flags {
        get {
            byte padded = Padded ? (byte) 0x8 : (byte) 0x0;
            byte endHeaders = EndHeaders ? (byte) 0x4 : (byte) 0x0;
            return (byte) (padded | endHeaders);
        }
    }
    public ushort PadLength {
        get => _padLength;
        set {
            if (value > 255) {
                throw new ArgumentOutOfRangeException(nameof(value), "Must be less than or equal to 255");
            }
            _padLength = value;
        }
    }
    public override IEnumerable<byte> Payload {
        get {
            List<byte> data = new List<byte>();
            if (Padded) {
                // Add the padding length
                data.Add((byte) _padLength);
            }

            // 1 Bit reserved as unset (0) so let's take the first bit of the next 32 bits and unset it
            data.AddRange(ConvertToUInt31(StreamDependency).EnsureBigEndian());

            if (HeaderBlockFragment != null && HeaderBlockFragment.Length > 0) {
                data.AddRange(HeaderBlockFragment);
            }

            // Add our padding
            for (int i = 0; i < _padLength; i++) {
                data.Add(0x0);
            }
            return data.ToArray();
        }
    }
    public uint StreamDependency { get; set; } = 0;
    public override FrameType Type => FrameType.PushPromise;

    public PushPromiseFrame() : base()
    {
    }

    public PushPromiseFrame(uint streamIdentifier) : base()
    {
        StreamIdentifier = streamIdentifier;
    }


    public override void ParsePayload(byte[] payloadData, FrameHeader frameHeader)
    {
        EndHeaders = (frameHeader.Flags & 0x4) == 0x4;
        Padded = (frameHeader.Flags & 0x8) == 0x8;

        int index = 0;

        if (Padded) {
            // Get pad length (1 byte)
            _padLength = (ushort) payloadData[index];
            index++;
        }
        else {
            _padLength = 0;
        }

        // Get Dependency Stream Id
        // we need to turn the stream id into a uint
        byte[] frameStreamIdData = new byte[4];
        Array.Copy(payloadData, index, frameStreamIdData, 0, 4);
        StreamDependency = ConvertFromUInt31(frameStreamIdData.EnsureBigEndian());

        // Advance the index
        index += 4;

        // create an array for the header data to read
        // it will be the payload length, minus the pad length value, weight, stream id, and padding
        HeaderBlockFragment = new byte[payloadData.Length - (index + _padLength)];
        Array.Copy(payloadData, index, HeaderBlockFragment, 0, HeaderBlockFragment.Length);

        // Advance the index
        index += HeaderBlockFragment.Length;

        // Don't care about padding
    }

    public override string ToString() => $"[Frame: PUSH_PROMISE, Id={StreamIdentifier}, EndStream={IsEndStream}, EndHeaders={EndHeaders}, StreamDependency={StreamDependency}, Padded={Padded}, PadLength={PadLength}, HeaderBlockFragmentLength={HeaderBlockFragment.Length}]";
}

using Netimobiledevice.Extentions;
using System;
using System.Collections.Generic;

namespace Netimobiledevice.Remoted.Frames;

internal class HeadersFrame : Frame, IFrameContainsHeaders
{
    private ushort padLength = 0;
    private ushort weight = 0;

    public bool Padded { get; set; }
    public bool EndStream { get; set; }
    public bool EndHeaders { get; set; }
    public bool Priority { get; set; }
    public byte[] HeaderBlockFragment { get; set; }

    public override byte Flags {
        get {
            byte endStream = EndStream ? (byte) 0x1 : (byte) 0x0;
            byte padded = Padded ? (byte) 0x8 : (byte) 0x0;
            byte endHeaders = EndHeaders ? (byte) 0x4 : (byte) 0x0;
            byte priority = Priority ? (byte) 0x20 : (byte) 0x0;

            return (byte) (endStream | padded | endHeaders | priority);
        }
    }
    public ushort PadLength {
        get => padLength;
        set {
            if (value > 255) {
                throw new ArgumentOutOfRangeException(nameof(value), "Must be less than or equal to 255");
            }
            padLength = value;
        }
    }
    public override IEnumerable<byte> Payload {
        get {
            var data = new List<byte>();

            if (Padded) {
                // Add the padding length
                data.Add((byte) padLength);
            }

            if (Priority) {
                // 1 Bit reserved as unset (0) so let's take the first bit of the next 32 bits and unset it
                data.AddRange(ConvertToUInt31(StreamDependency).EnsureBigEndian());

                // Weight
                int w = Priority ? weight : 0;
                data.Add((byte) w);
            }

            // Header Block Fragments
            if (HeaderBlockFragment != null && HeaderBlockFragment.Length > 0) {
                data.AddRange(HeaderBlockFragment);
            }

            // Add our padding

            for (int i = 0; i < padLength; i++) {
                data.Add(0x0);
            }
            return data.ToArray();
        }
    }

    public uint StreamDependency { get; set; } = 0;
    public override FrameType Type => FrameType.Headers;
    public ushort Weight {
        get => weight;
        set {
            if (value > 255) {
                throw new ArgumentOutOfRangeException(nameof(value), "Must be less than or equal to 255");
            }
            weight = value;
        }
    }

    public HeadersFrame() : base()
    {
    }

    public HeadersFrame(uint streamIdentifier) : base()
    {
        StreamIdentifier = streamIdentifier;
    }


    public override void ParsePayload(byte[] payloadData, FrameHeader frameHeader)
    {
        EndStream = (frameHeader.Flags & 0x1) == 0x1;
        EndHeaders = (frameHeader.Flags & 0x4) == 0x4;
        Priority = (frameHeader.Flags & 0x20) == 0x20;
        Padded = (frameHeader.Flags & 0x8) == 0x8;

        var index = 0;

        if (Padded) {
            // Get pad length (1 byte)
            padLength = payloadData[index];
            index++;
        }
        else {
            padLength = 0;
        }

        if (Priority) {
            // Get Dependency Stream Id
            // we need to turn the stream id into a uint
            byte[] frameStreamIdData = new byte[4];
            Array.Copy(payloadData, index, frameStreamIdData, 0, 4);
            StreamDependency = ConvertFromUInt31(frameStreamIdData.EnsureBigEndian());

            // Get the weight
            weight = payloadData[index + 4];

            // Advance the index
            index += 5;
        }


        // create an array for the header data to read
        // it will be the payload length, minus the pad length value, weight, stream id, and padding
        HeaderBlockFragment = new byte[payloadData.Length - (index + padLength)];
        Array.Copy(payloadData, index, HeaderBlockFragment, 0, HeaderBlockFragment.Length);

        // Advance the index
        index += HeaderBlockFragment.Length;

        // Don't care about padding
    }

    public override string ToString() => $"[Frame: HEADERS, Id={StreamIdentifier}, EndStream={IsEndStream}, EndHeaders={EndHeaders}, Priority={Priority}, Weight={Weight}, Padded={Padded}, PadLength={PadLength}, HeaderBlockFragmentLength={HeaderBlockFragment.Length}]";
}

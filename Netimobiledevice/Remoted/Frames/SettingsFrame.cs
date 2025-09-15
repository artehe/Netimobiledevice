using Netimobiledevice.Extentions;
using System;
using System.Collections.Generic;

namespace Netimobiledevice.Remoted.Frames;

internal class SettingsFrame : Frame
{
    public bool Ack { get; set; }

    public override FrameType Type => FrameType.Settings;

    public override byte Flags => Ack ? (byte) 0x1 : (byte) 0x0;

    public override uint StreamIdentifier => 0x0;

    public uint? HeaderTableSize { get; set; } // 4096 is default (0x1)
    public bool? EnablePush { get; set; } // 1 or 0 (0x2)
    public uint? MaxConcurrentStreams { get; set; } // (0x3)
    public uint? InitialWindowSize { get; set; } // (0x4)
    public uint? MaxFrameSize { get; set; } // (0x5)
    public uint? MaxHeaderListSize { get; set; } // (0x6)

    public override IEnumerable<byte> Payload {
        get {
            var data = new List<byte>();

            if (HeaderTableSize.HasValue) {
                data.AddRange(BitConverter.GetBytes((ushort) 0x1));
                data.AddRange(BitConverter.GetBytes(HeaderTableSize.Value));
            }

            if (EnablePush.HasValue) {
                data.AddRange(BitConverter.GetBytes((ushort) 0x2).EnsureBigEndian());
                data.AddRange(BitConverter.GetBytes(EnablePush.Value ? (uint) 1 : (uint) 0).EnsureBigEndian());
            }

            if (MaxConcurrentStreams.HasValue) {
                data.AddRange(BitConverter.GetBytes((ushort) 0x3).EnsureBigEndian());
                data.AddRange(BitConverter.GetBytes(MaxConcurrentStreams.Value).EnsureBigEndian());
            }

            if (InitialWindowSize.HasValue) {
                data.AddRange(BitConverter.GetBytes((ushort) 0x4).EnsureBigEndian());
                data.AddRange(BitConverter.GetBytes(InitialWindowSize.Value).EnsureBigEndian());
            }

            if (MaxFrameSize.HasValue) {
                data.AddRange(BitConverter.GetBytes((ushort) 0x5).EnsureBigEndian());
                data.AddRange(BitConverter.GetBytes(MaxFrameSize.Value));
            }

            if (MaxHeaderListSize.HasValue) {
                data.AddRange(BitConverter.GetBytes((ushort) 0x6).EnsureBigEndian());
                data.AddRange(BitConverter.GetBytes(MaxHeaderListSize.Value).EnsureBigEndian());
            }

            return data;
        }
    }

    public override void ParsePayload(byte[] payloadData, FrameHeader frameHeader)
    {
        Ack = (frameHeader.Flags & 0x1) == 0x1;
        for (int i = 0; i < payloadData.Length; i += 6) {
            uint value = BitConverter.ToUInt32(payloadData, i + 2);
            switch (value) {
                case 0x1:
                    HeaderTableSize = value;
                    break;
                case 0x2:
                    EnablePush = value == 1;
                    break;
                case 0x3:
                    MaxConcurrentStreams = value;
                    break;
                case 0x4:
                    InitialWindowSize = value;
                    break;
                case 0x5:
                    MaxFrameSize = value;
                    break;
                case 0x6:
                    MaxHeaderListSize = value;
                    break;
            }
        }
    }

    public override string ToString()
    {
        return $"[Frame: SETTINGS, Id={StreamIdentifier}, Ack={Ack}, HeaderTableSize={HeaderTableSize}, EnablePush={EnablePush}, MaxConcurrentStreams={MaxConcurrentStreams}, InitialWindowSize={InitialWindowSize}, MaxFrameSize={MaxFrameSize}, MaxHeaderListSize={MaxHeaderListSize}]";
    }

}

using Netimobiledevice.Extentions;
using System;
using System.Collections.Generic;

namespace Netimobiledevice.Remoted.Frames
{
    internal class GoAwayFrame : Frame
    {
        public uint LastStreamId { get; set; }
        public uint ErrorCode { get; set; }
        public byte[] AdditionalDebugData { get; set; }

        public override FrameType Type => FrameType.GoAway;

        public override IEnumerable<byte> Payload {
            get {
                var data = new List<byte>();

                // 1 Bit reserved as unset (0) so let's take the first bit of the next 32 bits and unset it
                data.AddRange(ConvertToUInt31(LastStreamId).EnsureBigEndian());

                data.AddRange(BitConverter.GetBytes(ErrorCode).EnsureBigEndian());

                if (AdditionalDebugData != null && AdditionalDebugData.Length > 0) {
                    data.AddRange(AdditionalDebugData);
                }
                return data;
            }
        }

        public override void ParsePayload(byte[] payloadData, FrameHeader frameHeader)
        {
            // we need to turn the stream id into a uint
            byte[] frameStreamIdData = new byte[4];
            Array.Copy(payloadData, 0, frameStreamIdData, 0, 4);
            LastStreamId = ConvertFromUInt31(frameStreamIdData.EnsureBigEndian());

            byte[] errorCodeData = new byte[4];
            Array.Copy(payloadData, 4, errorCodeData, 0, 4);
            uint errorCode = BitConverter.ToUInt32(errorCodeData.EnsureBigEndian(), 0);
            ErrorCode = errorCode;

            if (payloadData.Length > 8) {
                AdditionalDebugData = new byte[payloadData.Length - 8];
                Array.Copy(payloadData, 8, AdditionalDebugData, 0, payloadData.Length - 8);
            }
        }

        public override string ToString()
        {
            string debug = string.Empty;
            if (AdditionalDebugData != null && AdditionalDebugData.Length > 0) {
                debug = System.Text.Encoding.ASCII.GetString(AdditionalDebugData);
            }

            return $"[Frame: GOAWAY, Id={StreamIdentifier}, ErrorCode={ErrorCode}, LastStreamId={LastStreamId}, AdditionalDebugData={debug}]";
        }
    }
}
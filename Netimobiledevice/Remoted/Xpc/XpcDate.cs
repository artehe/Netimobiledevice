using System;

namespace Netimobiledevice.Remoted.Xpc;

/// <summary>
/// An XPC date object. On the wire this is nanoseconds since the Unix epoch,
/// stored as an unsigned, unaligned, unprefixed 64-bit little-endian integer.
/// </summary>
public class XpcDate(DateTime data) : XpcObject<DateTime>(data)
{
    private static readonly DateTime UnixEpoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public override bool IsAligned => false;

    public override bool IsPrefixed => false;

    public override XpcMessageType Type => XpcMessageType.Date;

    public static XpcDate Deserialise(byte[] data)
    {
        ulong nanoseconds = BitConverter.ToUInt64(data, 0);
        DateTime utc = UnixEpoch.AddTicks((long) (nanoseconds / 100));
        // Mirrors upstream pymobiledevice3, which converts via datetime.fromtimestamp()
        // and so hands back local wall-clock time rather than UTC.
        return new XpcDate(utc.ToLocalTime());
    }

    public override byte[] Serialise()
    {
        DateTime utc = Data.Kind == DateTimeKind.Utc ? Data : Data.ToUniversalTime();
        ulong nanoseconds = (ulong) ((utc - UnixEpoch).Ticks * 100);
        return BitConverter.GetBytes(nanoseconds);
    }
}

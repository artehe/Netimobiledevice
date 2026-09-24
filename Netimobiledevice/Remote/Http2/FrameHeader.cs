namespace Netimobiledevice.Remote.Http2;

internal readonly record struct FrameHeader(
    uint Length,
    FrameType Type,
    FrameFlags Flags,
    uint StreamIdentifier
);

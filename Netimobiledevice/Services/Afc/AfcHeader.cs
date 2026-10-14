namespace Netimobiledevice.Services.Afc;

/// <summary>The fixed 40-byte AFC packet header.</summary>
public readonly record struct AfcHeader(
    ulong EntireLength,
    ulong ThisLength,
    ulong PacketNum,
    AfcOpCode Operation
);

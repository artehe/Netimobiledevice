using System.Runtime.InteropServices;

namespace Netimobiledevice.EndianBitConversion;

// Converts between Single (float) and Int32 (int), as System.BitConverter does not have a method to do this in all .NET versions.
// A union is used instead of an unsafe pointer cast so we don't have to worry about the trusted environment implications.
[StructLayout(LayoutKind.Explicit)]
internal readonly struct SingleConverter
{
    // map int value to offset zero
    [FieldOffset(0)]
    private readonly int intValue;

    // map float value to offset zero - intValue and floatValue now take the same position in memory
    [FieldOffset(0)]
    private readonly float floatValue;

    internal SingleConverter(int intValue)
    {
        this.floatValue = 0;
        this.intValue = intValue;
    }

    internal SingleConverter(float floatValue)
    {
        this.intValue = 0;
        this.floatValue = floatValue;
    }

    internal readonly int GetIntValue() => this.intValue;

    internal readonly float GetFloatValue() => this.floatValue;
}

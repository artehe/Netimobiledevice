using Netimobiledevice.EndianBitConversion;

namespace NetimobiledeviceTest.EndianBitConversion;

[TestClass]
public class GetBytesTests
{
    private static EndianBitConverter machineEndianBitConverter;
    private static EndianBitConverter otherEndianBitConverter;

    private static void AssertArraysEqual(byte[] expected, byte[] actual)
    {
        Assert.IsNotNull(expected, "Expected value is null.");
        Assert.IsNotNull(actual, "Actual value is null.");
        Assert.AreEqual(expected.Length, actual.Length, "Length of array is incorrect.");
        for (int i = 0; i < expected.Length; i++) {
            Assert.AreEqual(expected[i], actual[i], $"Incorrect value at index {i}.");
        }
    }

    private static void AssertGetBytesResult<TInput>(
            Func<TInput, byte[]> bitConverterMethod,
            Func<TInput, byte[]> machineEndianBitConverterMethod,
            Func<TInput, byte[]> otherEndianBitConverterMethod,
            TInput testValue)
    {
        // compare endianness that matches the machine architecture
        byte[] expectedOutput = bitConverterMethod(testValue);
        byte[] machineEndianBitConverterOutput = machineEndianBitConverterMethod(testValue);
        AssertArraysEqual(expectedOutput, machineEndianBitConverterOutput);

        // compare other endianness by reversing the expected output of System.BitConverter
        expectedOutput = expectedOutput.Reverse().ToArray();
        byte[] otherEndianBitConverterOutput = otherEndianBitConverterMethod(testValue);
        AssertArraysEqual(expectedOutput, otherEndianBitConverterOutput);
    }

    [ClassInitialize()]
    public static void ClassInit(TestContext context)
    {
        if (BitConverter.IsLittleEndian) {
            machineEndianBitConverter = EndianBitConverter.LittleEndian;
            otherEndianBitConverter = EndianBitConverter.BigEndian;
        }
        else {
            machineEndianBitConverter = EndianBitConverter.BigEndian;
            otherEndianBitConverter = EndianBitConverter.LittleEndian;
        }
    }

    [TestMethod]
    public void GetBytesFromBool()
    {
        // Don't use System.BitConverter as an oracle for boolean true, as it can theoretically map to any non-zero byte
        AssertArraysEqual(new byte[] { 0x01 }, EndianBitConverter.BigEndian.GetBytes(true));
        AssertArraysEqual(new byte[] { 0x01 }, EndianBitConverter.LittleEndian.GetBytes(true));

        // Compare to System.BitConverter
        AssertGetBytesResult(BitConverter.GetBytes, machineEndianBitConverter.GetBytes, otherEndianBitConverter.GetBytes, false);
    }

    [TestMethod]
    public void GetBytesFromChar()
    {
        AssertGetBytesResult(BitConverter.GetBytes, machineEndianBitConverter.GetBytes, otherEndianBitConverter.GetBytes, '\0');
        AssertGetBytesResult(BitConverter.GetBytes, machineEndianBitConverter.GetBytes, otherEndianBitConverter.GetBytes, 'a');
        AssertGetBytesResult(BitConverter.GetBytes, machineEndianBitConverter.GetBytes, otherEndianBitConverter.GetBytes, '❤');
    }

    [TestMethod]
    public void GetBytesFromDouble()
    {
        AssertGetBytesResult(BitConverter.GetBytes, machineEndianBitConverter.GetBytes, otherEndianBitConverter.GetBytes, 0D);
        AssertGetBytesResult(BitConverter.GetBytes, machineEndianBitConverter.GetBytes, otherEndianBitConverter.GetBytes, 0.123456789e100);
        AssertGetBytesResult(BitConverter.GetBytes, machineEndianBitConverter.GetBytes, otherEndianBitConverter.GetBytes, double.Epsilon);
        AssertGetBytesResult(BitConverter.GetBytes, machineEndianBitConverter.GetBytes, otherEndianBitConverter.GetBytes, double.MaxValue);
        AssertGetBytesResult(BitConverter.GetBytes, machineEndianBitConverter.GetBytes, otherEndianBitConverter.GetBytes, double.MinValue);
        AssertGetBytesResult(BitConverter.GetBytes, machineEndianBitConverter.GetBytes, otherEndianBitConverter.GetBytes, double.NaN);
        AssertGetBytesResult(BitConverter.GetBytes, machineEndianBitConverter.GetBytes, otherEndianBitConverter.GetBytes, double.NegativeInfinity);
        AssertGetBytesResult(BitConverter.GetBytes, machineEndianBitConverter.GetBytes, otherEndianBitConverter.GetBytes, double.PositiveInfinity);
    }

    [TestMethod]
    public void GetBytesFromShort()
    {
        AssertGetBytesResult(BitConverter.GetBytes, machineEndianBitConverter.GetBytes, otherEndianBitConverter.GetBytes, (short) 0);
        AssertGetBytesResult(BitConverter.GetBytes, machineEndianBitConverter.GetBytes, otherEndianBitConverter.GetBytes, (short) 0x0123);
        AssertGetBytesResult(BitConverter.GetBytes, machineEndianBitConverter.GetBytes, otherEndianBitConverter.GetBytes, short.MaxValue);
        AssertGetBytesResult(BitConverter.GetBytes, machineEndianBitConverter.GetBytes, otherEndianBitConverter.GetBytes, short.MinValue);
    }

    [TestMethod]
    public void GetBytesFromInt()
    {
        AssertGetBytesResult(BitConverter.GetBytes, machineEndianBitConverter.GetBytes, otherEndianBitConverter.GetBytes, 0);
        AssertGetBytesResult(BitConverter.GetBytes, machineEndianBitConverter.GetBytes, otherEndianBitConverter.GetBytes, 0x01234567);
        AssertGetBytesResult(BitConverter.GetBytes, machineEndianBitConverter.GetBytes, otherEndianBitConverter.GetBytes, int.MaxValue);
        AssertGetBytesResult(BitConverter.GetBytes, machineEndianBitConverter.GetBytes, otherEndianBitConverter.GetBytes, int.MinValue);
    }

    [TestMethod]
    public void GetBytesFromLong()
    {
        AssertGetBytesResult(BitConverter.GetBytes, machineEndianBitConverter.GetBytes, otherEndianBitConverter.GetBytes, 0L);
        AssertGetBytesResult(BitConverter.GetBytes, machineEndianBitConverter.GetBytes, otherEndianBitConverter.GetBytes, 0x0123456789ABCDEF);
        AssertGetBytesResult(BitConverter.GetBytes, machineEndianBitConverter.GetBytes, otherEndianBitConverter.GetBytes, long.MaxValue);
        AssertGetBytesResult(BitConverter.GetBytes, machineEndianBitConverter.GetBytes, otherEndianBitConverter.GetBytes, long.MinValue);
    }

    [TestMethod]
    public void GetBytesFromFloat()
    {
        AssertGetBytesResult(BitConverter.GetBytes, machineEndianBitConverter.GetBytes, otherEndianBitConverter.GetBytes, 0F);
        AssertGetBytesResult(BitConverter.GetBytes, machineEndianBitConverter.GetBytes, otherEndianBitConverter.GetBytes, 0.123456e10F);
        AssertGetBytesResult(BitConverter.GetBytes, machineEndianBitConverter.GetBytes, otherEndianBitConverter.GetBytes, float.Epsilon);
        AssertGetBytesResult(BitConverter.GetBytes, machineEndianBitConverter.GetBytes, otherEndianBitConverter.GetBytes, float.MaxValue);
        AssertGetBytesResult(BitConverter.GetBytes, machineEndianBitConverter.GetBytes, otherEndianBitConverter.GetBytes, float.MinValue);
        AssertGetBytesResult(BitConverter.GetBytes, machineEndianBitConverter.GetBytes, otherEndianBitConverter.GetBytes, float.NaN);
        AssertGetBytesResult(BitConverter.GetBytes, machineEndianBitConverter.GetBytes, otherEndianBitConverter.GetBytes, float.NegativeInfinity);
        AssertGetBytesResult(BitConverter.GetBytes, machineEndianBitConverter.GetBytes, otherEndianBitConverter.GetBytes, float.PositiveInfinity);
    }

    [TestMethod]
    public void GetBytesFromUShort()
    {
        AssertGetBytesResult(BitConverter.GetBytes, machineEndianBitConverter.GetBytes, otherEndianBitConverter.GetBytes, (ushort) 0);
        AssertGetBytesResult(BitConverter.GetBytes, machineEndianBitConverter.GetBytes, otherEndianBitConverter.GetBytes, (ushort) 0x0123);
        AssertGetBytesResult(BitConverter.GetBytes, machineEndianBitConverter.GetBytes, otherEndianBitConverter.GetBytes, ushort.MaxValue);
    }

    [TestMethod]
    public void GetBytesFromUInt()
    {
        AssertGetBytesResult(BitConverter.GetBytes, machineEndianBitConverter.GetBytes, otherEndianBitConverter.GetBytes, 0U);
        AssertGetBytesResult(BitConverter.GetBytes, machineEndianBitConverter.GetBytes, otherEndianBitConverter.GetBytes, 0x01234567U);
        AssertGetBytesResult(BitConverter.GetBytes, machineEndianBitConverter.GetBytes, otherEndianBitConverter.GetBytes, uint.MaxValue);
    }

    [TestMethod]
    public void GetBytesFromULong()
    {
        AssertGetBytesResult(BitConverter.GetBytes, machineEndianBitConverter.GetBytes, otherEndianBitConverter.GetBytes, 0uL);
        AssertGetBytesResult(BitConverter.GetBytes, machineEndianBitConverter.GetBytes, otherEndianBitConverter.GetBytes, 0x0123456789ABCDEFuL);
        AssertGetBytesResult(BitConverter.GetBytes, machineEndianBitConverter.GetBytes, otherEndianBitConverter.GetBytes, ulong.MaxValue);
    }
}

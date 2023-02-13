using Netimobiledevice.EndianBitConversion;

namespace NetimobiledeviceTest.EndianBitConversion;

[TestClass]
public class GetValueTests
{
    private static EndianBitConverter machineEndianBitConverter;
    private static EndianBitConverter otherEndianBitConverter;

    private static void AssertEightByteValueResult<TOutput>(
            Func<byte[], int, TOutput> bitConverterMethod,
            Func<byte[], int, TOutput> machineEndianBitConverterMethod,
            Func<byte[], int, TOutput> otherEndianBitConverterMethod)
    {
        ValidateArgumentsChecks(machineEndianBitConverterMethod, otherEndianBitConverterMethod, 8);

        AssertValueResult(bitConverterMethod, machineEndianBitConverterMethod, otherEndianBitConverterMethod,
            8, new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07 }, 0);
        AssertValueResult(bitConverterMethod, machineEndianBitConverterMethod, otherEndianBitConverterMethod,
            8, new byte[] { 0x00, 0x01, 0x23, 0x45, 0x67, 0x89, 0xAB, 0xCD, 0xEF }, 1);
    }

    private static void AssertFourByteValueResult<TOutput>(
            Func<byte[], int, TOutput> bitConverterMethod,
            Func<byte[], int, TOutput> machineEndianBitConverterMethod,
            Func<byte[], int, TOutput> otherEndianBitConverterMethod)
    {
        ValidateArgumentsChecks(machineEndianBitConverterMethod, otherEndianBitConverterMethod, 4);

        AssertValueResult(bitConverterMethod, machineEndianBitConverterMethod, otherEndianBitConverterMethod,
            4, new byte[] { 0x00, 0x01, 0x02, 0x03 }, 0);
        AssertValueResult(bitConverterMethod, machineEndianBitConverterMethod, otherEndianBitConverterMethod,
            4, new byte[] { 0x67, 0x89, 0xAB, 0xCD, 0xEF }, 1);
    }

    private static void AssertTwoByteValueResult<TOutput>(
            Func<byte[], int, TOutput> bitConverterMethod,
            Func<byte[], int, TOutput> machineEndianBitConverterMethod,
            Func<byte[], int, TOutput> otherEndianBitConverterMethod)
    {
        ValidateArgumentsChecks(machineEndianBitConverterMethod, otherEndianBitConverterMethod, 2);

        AssertValueResult(bitConverterMethod, machineEndianBitConverterMethod, otherEndianBitConverterMethod,
            2, new byte[] { 0x00, 0x01 }, 0);
        AssertValueResult(bitConverterMethod, machineEndianBitConverterMethod, otherEndianBitConverterMethod,
            2, new byte[] { 0x00, 0xAB, 0x00 }, 1);
    }

    private static void AssertValueResult<TOutput>(
            Func<byte[], int, TOutput> bitConverterMethod,
            Func<byte[], int, TOutput> machineEndianBitConverterMethod,
            Func<byte[], int, TOutput> otherEndianBitConverterMethod,
            int outputSize,
            byte[] testValue,
            int testStartIndex)
    {
        // Compare endianness that matches the machine architecture
        TOutput expectedOutput = bitConverterMethod(testValue, testStartIndex);
        TOutput machineEndianBitConverterOutput = machineEndianBitConverterMethod(testValue, testStartIndex);
        Assert.AreEqual(expectedOutput, machineEndianBitConverterOutput);

        // Compare other endianness by reversing the input to System.BitConverter
        TOutput otherEndianBitConverterOutput = otherEndianBitConverterMethod(testValue, testStartIndex);
        testValue = testValue.Reverse().ToArray();
        testStartIndex = testValue.Length - testStartIndex - outputSize;
        expectedOutput = bitConverterMethod(testValue, testStartIndex);
        Assert.AreEqual(expectedOutput, otherEndianBitConverterOutput);
    }

    private static void ValidateArgumentsChecks<TOutput>(
            Func<byte[], int, TOutput> machineEndianBitConverterMethod,
            Func<byte[], int, TOutput> otherEndianBitConverterMethod,
            int outputSize)
    {
        // Null check
        Assert.ThrowsException<ArgumentNullException>(() => machineEndianBitConverterMethod(null, 0));
        Assert.ThrowsException<ArgumentNullException>(() => otherEndianBitConverterMethod(null, 0));

        // Negative index
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => machineEndianBitConverterMethod(new byte[8], -1));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => otherEndianBitConverterMethod(new byte[8], -1));

        // Index + outputSize longer than byte array
        const int arrayLength = 16;
        int badStartIndex = arrayLength - outputSize + 1;
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => machineEndianBitConverterMethod(new byte[arrayLength], badStartIndex));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => otherEndianBitConverterMethod(new byte[arrayLength], badStartIndex));
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
    public void OneByte()
    {
        ValidateArgumentsChecks(machineEndianBitConverter.ToBoolean, otherEndianBitConverter.ToBoolean, sizeof(bool));

        AssertValueResult(BitConverter.ToBoolean, machineEndianBitConverter.ToBoolean, otherEndianBitConverter.ToBoolean,
            sizeof(bool), new byte[] { 0x00, 0x01, 0x00 }, 0);
        AssertValueResult(BitConverter.ToBoolean, machineEndianBitConverter.ToBoolean, otherEndianBitConverter.ToBoolean,
            sizeof(bool), new byte[] { 0x00, 0x01, 0x00 }, 1);
        AssertValueResult(BitConverter.ToBoolean, machineEndianBitConverter.ToBoolean, otherEndianBitConverter.ToBoolean,
            sizeof(bool), new byte[] { 0x00, 0x00, 0xFE }, 2);
    }

    [TestMethod]
    public void TwoBytes()
    {
        AssertTwoByteValueResult(BitConverter.ToChar, machineEndianBitConverter.ToChar, otherEndianBitConverter.ToChar);
        AssertTwoByteValueResult(BitConverter.ToInt16, machineEndianBitConverter.ToInt16, otherEndianBitConverter.ToInt16);
        AssertTwoByteValueResult(BitConverter.ToUInt16, machineEndianBitConverter.ToUInt16, otherEndianBitConverter.ToUInt16);
    }

    [TestMethod]
    public void FourBytes()
    {
        AssertFourByteValueResult(BitConverter.ToInt32, machineEndianBitConverter.ToInt32, otherEndianBitConverter.ToInt32);
        AssertFourByteValueResult(BitConverter.ToUInt32, machineEndianBitConverter.ToUInt32, otherEndianBitConverter.ToUInt32);
        AssertFourByteValueResult(BitConverter.ToSingle, machineEndianBitConverter.ToSingle, otherEndianBitConverter.ToSingle);
    }

    [TestMethod]
    public void EightBytes()
    {
        AssertEightByteValueResult(BitConverter.ToInt64, machineEndianBitConverter.ToInt64, otherEndianBitConverter.ToInt64);
        AssertEightByteValueResult(BitConverter.ToUInt64, machineEndianBitConverter.ToUInt64, otherEndianBitConverter.ToUInt64);
        AssertEightByteValueResult(BitConverter.ToDouble, machineEndianBitConverter.ToDouble, otherEndianBitConverter.ToDouble);
    }
}

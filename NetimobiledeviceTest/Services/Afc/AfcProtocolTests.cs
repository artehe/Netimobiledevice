using Netimobiledevice.Services.Afc;
using System.Buffers.Binary;
using System.Text;

namespace NetimobiledeviceTest.Services.Afc;

[TestClass]
public sealed class AfcProtocolTests {
    private static ulong BinaryLe(byte[] data, int offset) => BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(offset));

    [TestMethod]
    public void Concat_NoParts_ReturnsEmpty() {
        byte[] result = AfcProtocol.Concat();
        Assert.IsEmpty(result);
    }

    [TestMethod]
    public void Concat_SinglePart_ReturnsCopy() {
        byte[] part = [1, 2, 3];

        byte[] result = AfcProtocol.Concat(part);

        Assert.AreSequenceEqual(part, result);
        Assert.AreNotSame(part, result);
    }

    [TestMethod]
    public void Concat_MultipleParts_JoinsInOrder() {
        byte[] result = AfcProtocol.Concat([1, 2], [3], [4, 5, 6]);
        Assert.AreSequenceEqual(new byte[] { 1, 2, 3, 4, 5, 6 }, result);
    }

    [TestMethod]
    public void Concat_EmptyPartsAreSkipped() {
        byte[] result = AfcProtocol.Concat([], [1], [], [2], []);
        Assert.AreSequenceEqual(new byte[] { 1, 2 }, result);
    }

    [TestMethod]
    public void Concat_DoesNotModifyInputs() {
        byte[] a = [1, 2];
        byte[] b = [3, 4];

        byte[] result = AfcProtocol.Concat(a, b);
        result[0] = 99;

        Assert.AreSequenceEqual(new byte[] { 1, 2 }, a);
        Assert.AreSequenceEqual(new byte[] { 3, 4 }, b);
    }

    [TestMethod]
    public void BuildPacket_WritesHeaderAndPayload() {
        byte[] payload = [0xAA, 0xBB, 0xCC];

        byte[] packet = AfcProtocol.BuildPacket(7, (AfcOpCode) 5, payload);

        byte[] expected = [
            // Magic
            .. AfcProtocol.Magic,
            // Entire length = 43
            0x2B, 0, 0, 0, 0, 0, 0, 0,
            // This length defaults to entire length
            0x2B, 0, 0, 0, 0, 0, 0, 0,
            // Packet number
            0x07, 0, 0, 0, 0, 0, 0, 0,
            // Operation
            0x05, 0, 0, 0, 0, 0, 0, 0,
            // Payload
            0xAA, 0xBB, 0xCC,
        ];
        Assert.AreSequenceEqual(expected, packet);
    }

    [TestMethod]
    public void BuildPacket_EmptyPayload_IsHeaderOnly() {
        byte[] packet = AfcProtocol.BuildPacket(0, (AfcOpCode) 1, []);

        Assert.HasCount(AfcProtocol.HeaderSize, packet);
        Assert.AreSequenceEqual(AfcProtocol.Magic.ToArray(), packet[..8]);
        Assert.AreEqual(40UL, BinaryLe(packet, 8));
        Assert.AreEqual(40UL, BinaryLe(packet, 16));
    }

    [TestMethod]
    public void BuildPacket_ExplicitThisLength_IsUsed() {
        byte[] packet = AfcProtocol.BuildPacket(1, (AfcOpCode) 1, new byte[10], thisLength: 48);

        Assert.AreEqual(50UL, BinaryLe(packet, 8), "entire length");
        Assert.AreEqual(48UL, BinaryLe(packet, 16), "this length");
    }

    [TestMethod]
    public void BuildPacket_LargePacketNumber_IsLittleEndian() {
        byte[] packet = AfcProtocol.BuildPacket(0x0102030405060708UL, (AfcOpCode) 1, []);

        Assert.AreSequenceEqual(
            new byte[] { 0x08, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01 }, packet[24..32]
        );
    }

    [TestMethod]
    public void BuildPacket_RoundTripsThroughParseHeader() {
        byte[] payload = [1, 2, 3, 4];

        byte[] packet = AfcProtocol.BuildPacket(42, (AfcOpCode) 9, payload, thisLength: 41);
        AfcHeader header = AfcProtocol.ParseHeader(packet);

        Assert.AreEqual(new AfcHeader(44, 41, 42, (AfcOpCode) 9), header);
    }

    [TestMethod]
    [DataRow(0L, new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 })]
    [DataRow(1L, new byte[] { 1, 0, 0, 0, 0, 0, 0, 0 })]
    [DataRow(-1L, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF })]
    [DataRow(0x0102030405060708L, new byte[] { 0x08, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01 })]
    [DataRow(long.MaxValue, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F })]
    [DataRow(long.MinValue, new byte[] { 0, 0, 0, 0, 0, 0, 0, 0x80 })]
    public void I64_WritesLittleEndian(long value, byte[] expected) {
        Assert.AreSequenceEqual(expected, AfcProtocol.I64(value));
    }

    [TestMethod]
    [DataRow(0UL, new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 })]
    [DataRow(1UL, new byte[] { 1, 0, 0, 0, 0, 0, 0, 0 })]
    [DataRow(0x0102030405060708UL, new byte[] { 0x08, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01 })]
    [DataRow(ulong.MaxValue, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF })]
    public void U64_WritesLittleEndian(ulong value, byte[] expected) {
        Assert.AreSequenceEqual(expected, AfcProtocol.U64(value));
    }

    [TestMethod]
    public void ListToDict_ParsesPairs() {
        Dictionary<string, string> result = AfcProtocol.ListToDict(Encoding.UTF8.GetBytes("st_size\0123\0st_ifmt\0S_IFREG\0"));

        Assert.HasCount(2, result);
        Assert.AreEqual("123", result["st_size"]);
        Assert.AreEqual("S_IFREG", result["st_ifmt"]);
    }

    [TestMethod]
    public void ListToDict_Empty_ReturnsEmptyDictionary() {
        Assert.IsEmpty(AfcProtocol.ListToDict([]));
    }

    [TestMethod]
    public void ListToDict_EmptyValue_IsAllowed() {
        Dictionary<string, string> result = AfcProtocol.ListToDict(Encoding.UTF8.GetBytes("key\0\0"));

        Assert.HasCount(1, result);
        Assert.AreEqual("", result["key"]);
    }

    [TestMethod]
    public void ListToDict_DuplicateKey_LastValueWins() {
        Dictionary<string, string> result = AfcProtocol.ListToDict(Encoding.UTF8.GetBytes("k\0first\0k\0second\0"));

        Assert.HasCount(1, result);
        Assert.AreEqual("second", result["k"]);
    }

    [TestMethod]
    public void ListToDict_IgnoresDataAfterFinalTerminator() {
        Dictionary<string, string> result = AfcProtocol.ListToDict(Encoding.UTF8.GetBytes("k\0v\0trailing"));

        Assert.HasCount(1, result);
        Assert.AreEqual("v", result["k"]);
    }

    [TestMethod]
    public void ListToDict_NonAsciiUtf8_IsDecoded() {
        Dictionary<string, string> result = AfcProtocol.ListToDict(Encoding.UTF8.GetBytes("名前\0café\0"));
        Assert.AreEqual("café", result["名前"]);
    }

    [TestMethod]
    [DataRow("k\0", DisplayName = "Key without value")]
    [DataRow("k\0v", DisplayName = "Unterminated value")]
    [DataRow("a\0b\0c\0", DisplayName = "Odd number of entries")]
    public void ListToDict_Misaligned_Throws(string raw) {
        Assert.ThrowsExactly<FormatException>(() => AfcProtocol.ListToDict(Encoding.UTF8.GetBytes(raw)));
    }

    [TestMethod]
    public void ListToDict_InvalidUtf8_Throws() {
        byte[] raw = [(byte) 'k', 0, 0xFF, 0xFE, 0];
        Assert.ThrowsExactly<DecoderFallbackException>(() => AfcProtocol.ListToDict(raw));
    }

    [TestMethod]
    [DataRow("a\0b\0", new[] { "a", "b" }, DisplayName = "Terminated strings")]
    [DataRow("a\0b", new[] { "a" }, DisplayName = "Unterminated tail dropped")]
    [DataRow("abc", new string[0], DisplayName = "No terminator")]
    [DataRow("", new string[0], DisplayName = "Empty")]
    [DataRow("\0", new[] { "" }, DisplayName = "Single empty string")]
    [DataRow("\0\0", new[] { "", "" }, DisplayName = "Two empty strings")]
    [DataRow(".\0..\0file.txt\0", new[] { ".", "..", "file.txt" }, DisplayName = "Directory listing")]
    [DataRow("über\0日本\0", new[] { "über", "日本" }, DisplayName = "Non-ASCII")]
    public void ParseCStrings_SplitsOnNul(string raw, string[] expected) {
        Assert.AreSequenceEqual(expected, AfcProtocol.ParseCStrings(Encoding.UTF8.GetBytes(raw)));
    }

    [TestMethod]
    public void ParseCStrings_InvalidUtf8_Throws() {
        byte[] raw = [0xC3, 0x28, 0];
        Assert.ThrowsExactly<DecoderFallbackException>(() => AfcProtocol.ParseCStrings(raw));
    }

    [TestMethod]
    public void ParseHeader_ReadsFields() {
        byte[] data = [
            .. AfcProtocol.Magic,
            0x30, 0, 0, 0, 0, 0, 0, 0,
            0x28, 0, 0, 0, 0, 0, 0, 0,
            0x03, 0x01, 0, 0, 0, 0, 0, 0,
            0x0A, 0, 0, 0, 0, 0, 0, 0,
        ];

        AfcHeader header = AfcProtocol.ParseHeader(data);

        Assert.AreEqual(new AfcHeader(0x30, 0x28, 0x0103, (AfcOpCode) 0x0A), header);
    }

    [TestMethod]
    public void ParseHeader_IgnoresBytesAfterHeader() {
        byte[] packet = AfcProtocol.BuildPacket(3, (AfcOpCode) 2, [9, 9, 9]);

        AfcHeader header = AfcProtocol.ParseHeader(packet);

        Assert.AreEqual(new AfcHeader(43, 43, 3, (AfcOpCode) 2), header);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(8)]
    [DataRow(39)]
    public void ParseHeader_Truncated_Throws(int length) {
        byte[] data = AfcProtocol.BuildPacket(1, (AfcOpCode) 1, [])[..length];

        AfcException ex = Assert.ThrowsExactly<AfcException>(() => AfcProtocol.ParseHeader(data));
        Assert.Contains("Truncated", ex.Message);
    }

    [TestMethod]
    public void ParseHeader_BadMagic_Throws() {
        byte[] data = AfcProtocol.BuildPacket(1, (AfcOpCode) 1, []);
        data[0] = (byte) 'X';

        AfcException ex = Assert.ThrowsExactly<AfcException>(() => AfcProtocol.ParseHeader(data));
        Assert.Contains("magic", ex.Message);
    }

    [TestMethod]
    [DataRow("0", 0L)]
    [DataRow("123", 123L)]
    [DataRow("-5", -5L)]
    [DataRow("+7", 7L)]
    [DataRow(" 42 ", 42L, DisplayName = "Surrounding whitespace")]
    [DataRow("9223372036854775807", long.MaxValue)]
    [DataRow("-9223372036854775808", long.MinValue)]
    public void ParseLong_ParsesIntegers(string value, long expected) {
        Assert.AreEqual(expected, AfcProtocol.ParseLong(value));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("abc")]
    [DataRow("1.5")]
    [DataRow("1,000")]
    [DataRow("0x10")]
    public void ParseLong_Invalid_ThrowsFormatException(string value) {
        Assert.ThrowsExactly<FormatException>(() => AfcProtocol.ParseLong(value));
    }

    [TestMethod]
    [DataRow("9223372036854775808")]
    [DataRow("-9223372036854775809")]
    public void ParseLong_OutOfRange_ThrowsOverflowException(string value) {
        Assert.ThrowsExactly<OverflowException>(() => AfcProtocol.ParseLong(value));
    }

    [TestMethod]
    public void ParseLong_Null_ThrowsArgumentNullException() {
        Assert.ThrowsExactly<ArgumentNullException>(() => AfcProtocol.ParseLong(null!));
    }

    [TestMethod]
    [DataRow(0UL)]
    [DataRow(1UL)]
    [DataRow(8UL)]
    public void ParseStatus_ReadsLittleEndianCode(ulong code) {
        Assert.AreEqual((AfcError) code, AfcProtocol.ParseStatus(AfcProtocol.U64(code)));
    }

    [TestMethod]
    public void ParseStatus_IgnoresBytesAfterFirstEight() {
        byte[] data = [.. AfcProtocol.U64(3), 0xFF, 0xFF];
        Assert.AreEqual((AfcError) 3, AfcProtocol.ParseStatus(data));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(7)]
    public void ParseStatus_Truncated_Throws(int length) {
        byte[] data = new byte[length];

        AfcException ex = Assert.ThrowsExactly<AfcException>(() => AfcProtocol.ParseStatus(data));
        Assert.Contains("Truncated", ex.Message);
    }
}

using Netimobiledevice;
using Netimobiledevice.Remote.Http2;
using Netimobiledevice.Remoted.Frames;
using System.Buffers.Binary;

namespace NetimobiledeviceTest.Remote.Http2;

[TestClass]
public class FrameTests {
    [TestMethod]
    public void Create_AllSupportedFrameTypes_ReturnsExpectedFrame() {
        (FrameType Type, Type ExpectedType)[] cases = [
            (FrameType.Data, typeof(DataFrame)),
            (FrameType.Headers, typeof(HeadersFrame)),
            (FrameType.Priority, typeof(PriorityFrame)),
            (FrameType.RstStream, typeof(RstStreamFrame)),
            (FrameType.Settings, typeof(SettingsFrame)),
            (FrameType.PushPromise, typeof(PushPromiseFrame)),
            (FrameType.Ping, typeof(PingFrame)),
            (FrameType.GoAway, typeof(GoAwayFrame)),
            (FrameType.WindowUpdate, typeof(WindowUpdateFrame)),
            (FrameType.Continuation, typeof(ContinuationFrame)),
        ];
        foreach ((FrameType type, Type? expectedType) in cases) {
            Frame frame = Frame.Create(type);

            Assert.IsNotNull(frame);
            Assert.AreEqual(expectedType, frame.GetType());
            Assert.AreEqual(type, frame.Type);
        }
    }

    [TestMethod]
    public void Create_ByteFrameType_ReturnsExpectedFrame() {
        Frame frame = Frame.Create((byte) FrameType.Data);

        Assert.IsInstanceOfType<DataFrame>(frame);
        Assert.AreEqual(FrameType.Data, frame.Type);
    }

    [TestMethod]
    public void Create_UnknownFrameType_ThrowsNetimobiledeviceException() {
        const byte unknownType = 0xFF;
        Assert.ThrowsExactly<NetimobiledeviceException>(() => Frame.Create(unknownType));
    }

    [TestMethod]
    public void ParseFrameHeader_ValidHeader_ReturnsExpectedValues() {
        // Length = 5
        // Type = Data
        // Flags = EndStream
        // Stream ID = 1
        byte[] data = [
            0x00, 0x00, 0x05,
            (byte)FrameType.Data,
            (byte)FrameFlags.EndStream,
            0x00, 0x00, 0x00, 0x01,

            // Payload
            0x01, 0x02, 0x03, 0x04, 0x05
        ];

        FrameHeader header = Frame.ParseFrameHeader(data);

        Assert.AreEqual(5u, header.Length);
        Assert.AreEqual(FrameType.Data, header.Type);
        Assert.AreEqual(FrameFlags.EndStream, header.Flags);
        Assert.AreEqual(1u, header.StreamIdentifier);
    }

    [TestMethod]
    public void ParseFrameHeader_ZeroLengthPayload_IsValid() {
        byte[] data = [
            0x00, 0x00, 0x00,
            (byte)FrameType.Ping,
            0x00,
            0x00, 0x00, 0x00, 0x00
        ];

        FrameHeader header = Frame.ParseFrameHeader(data);

        Assert.AreEqual(0u, header.Length);
        Assert.AreEqual(FrameType.Ping, header.Type);
        Assert.AreEqual(0, (byte) header.Flags);
        Assert.AreEqual(0u, header.StreamIdentifier);
    }

    [TestMethod]
    public void ParseFrameHeader_LessThanNineBytes_ThrowsInvalidDataException() {
        byte[] data = new byte[8];
        Assert.ThrowsExactly<InvalidDataException>(() => Frame.ParseFrameHeader(data));
    }

    [TestMethod]
    public void ParseFrameHeader_DeclaredPayloadLargerThanAvailable_ThrowsInvalidDataException() {
        // Declares a 10-byte payload but provides no payload.
        byte[] data = [
            0x00, 0x00, 0x0A,
            (byte)FrameType.Data,
            0x00,
            0x00, 0x00, 0x00, 0x01
        ];

        InvalidDataException exception = Assert.ThrowsExactly<InvalidDataException>(() => Frame.ParseFrameHeader(data));
        Assert.Contains("10-byte payload", exception.Message);
    }

    [TestMethod]
    public void ParseFrameHeader_ReservedStreamIdBit_IsMasked() {
        byte[] data = [
            0x00, 0x00, 0x00,
            (byte)FrameType.Data,
            0x00,
            // 0x80000001 has the reserved MSB set.
            0x80, 0x00, 0x00, 0x01
        ];

        FrameHeader header = Frame.ParseFrameHeader(data);
        Assert.AreEqual(1u, header.StreamIdentifier);
    }

    [TestMethod]
    public void Parse_ValidFrame_SetsHeaderProperties() {
        // Data frame with a 3-byte payload.
        byte[] data = [
            0x00, 0x00, 0x03,
            (byte)FrameType.Data,
            (byte)FrameFlags.EndStream,
            0x00, 0x00, 0x00, 0x07,

            0xAA, 0xBB, 0xCC
        ];

        Frame frame = Frame.Create(FrameType.Data);
        frame.Parse(data);

        Assert.AreEqual(FrameType.Data, frame.Type);
        Assert.AreEqual(FrameFlags.EndStream, frame.Flags);
        Assert.AreEqual(7u, frame.StreamIdentifier);
        Assert.AreEqual(3u, frame.PayloadLength);
        Assert.IsTrue(frame.IsEndStream);
    }

    [TestMethod]
    public void ToBytes_ProducesExpectedHttp2Frame() {
        Frame frame = Frame.Create(FrameType.Data);

        frame.StreamIdentifier = 1;
        frame.Flags = FrameFlags.EndStream;

        byte[] bytes = frame.ToBytes();

        Assert.HasCount(9, bytes);

        Assert.AreEqual(0x00, bytes[0]);
        Assert.AreEqual(0x00, bytes[1]);
        Assert.AreEqual(0x00, bytes[2]);
        Assert.AreEqual((byte) FrameType.Data, bytes[3]);
        Assert.AreEqual((byte) FrameFlags.EndStream, bytes[4]);

        uint streamId = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(5, 4));
        Assert.AreEqual(1u, streamId);
    }

    [TestMethod]
    public void ToBytes_StreamIdentifierWithReservedBit_Throws() {
        Frame frame = Frame.Create(FrameType.Data);

        // Greater than 0x7FFFFFFF.
        frame.StreamIdentifier = 0x80000000;

        Assert.ThrowsExactly<InvalidOperationException>(() => frame.ToBytes());
    }

    [TestMethod]
    public void IsEndStream_DataFrameWithEndStreamFlag_ReturnsTrue() {
        Frame frame = Frame.Create(FrameType.Data);
        frame.Flags = FrameFlags.EndStream;

        Assert.IsTrue(frame.IsEndStream);
    }

    [TestMethod]
    public void IsEndStream_DataFrameWithoutEndStreamFlag_ReturnsFalse() {
        Frame frame = Frame.Create(FrameType.Data);
        frame.Flags = 0;

        Assert.IsFalse(frame.IsEndStream);
    }

    [TestMethod]
    public void IsEndStream_HeadersFrameWithEndStreamFlag_ReturnsTrue() {
        Frame frame = Frame.Create(FrameType.Headers);
        frame.Flags = FrameFlags.EndStream;

        Assert.IsTrue(frame.IsEndStream);
    }

    [TestMethod]
    public void IsEndStream_HeadersFrameWithoutEndStreamFlag_ReturnsFalse() {
        Frame frame = Frame.Create(FrameType.Headers);
        frame.Flags = 0;

        Assert.IsFalse(frame.IsEndStream);
    }

    [TestMethod]
    public void IsEndStream_NonDataOrHeadersFrame_ReturnsFalse() {
        Frame frame = Frame.Create(FrameType.Ping);
        frame.Flags = FrameFlags.EndStream;

        Assert.IsFalse(frame.IsEndStream);
    }

    [TestMethod]
    public void ParseThenToBytes_PreservesFrameHeader() {
        byte[] original = [
            0x00, 0x00, 0x03,
            (byte)FrameType.Data,
            (byte)FrameFlags.EndStream,
            0x00, 0x00, 0x00, 0x05,
            0xAA, 0xBB, 0xCC
        ];

        Frame frame = Frame.Create(FrameType.Data);
        frame.Parse(original);

        byte[] result = frame.ToBytes();
        Assert.AreSequenceEqual(original, result);
    }
}

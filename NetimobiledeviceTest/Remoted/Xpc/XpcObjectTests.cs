using Netimobiledevice.Remoted.Xpc;

namespace NetimobiledeviceTest.Remoted.Xpc;

/// <summary>
/// Round-trip tests for the previously-stubbed XPC wire types (Data, Date, Fd, Shmem,
/// FileTransfer). Every expected byte array below was generated from upstream
/// pymobiledevice3's actual construct-based codec (the Python source this port is
/// based on), so a pass here means byte-exact parity with the real XPC wire format.
/// </summary>
[TestClass]
public class XpcObjectTests {
    private static void AssertBytesEqual(byte[] expected, byte[] actual) {
        Assert.HasCount(expected.Length, actual, "Byte array lengths differ");
        for (int i = 0; i < expected.Length; i++) {
            Assert.AreEqual(expected[i], actual[i], $"Byte mismatch at index {i}");
        }
    }

    [TestMethod]
    public void XpcData_RoundTrips_EmptyPayload() {
        byte[] wireBytes = [0x0, 0x80, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0];

        XpcObject obj = XpcSerialiser.Deserialise(wireBytes);
        XpcData data = (XpcData) obj;

        Assert.AreEqual(XpcMessageType.Data, data.Type);
        Assert.AreSequenceEqual([], data.Data);
        AssertBytesEqual(wireBytes, XpcSerialiser.Serialise(data));
    }

    [TestMethod]
    public void XpcData_RoundTrips_PayloadNeedingTwoBytesPadding() {
        // 2 payload bytes -> 4(prefix)+2(data)=6, padded up to 8
        byte[] wireBytes = [0x0, 0x80, 0x0, 0x0, 0x2, 0x0, 0x0, 0x0, 0x61, 0x62, 0x0, 0x0];

        XpcData data = (XpcData) XpcSerialiser.Deserialise(wireBytes);

        Assert.AreSequenceEqual((byte[]) [0x61, 0x62], data.Data);
        AssertBytesEqual(wireBytes, XpcSerialiser.Serialise(data));
    }

    [TestMethod]
    public void XpcData_RoundTrips_PayloadNeedingNoPadding() {
        // 6 payload bytes -> 4(prefix)+6(data)=10, padded up to 12
        byte[] wireBytes = [
            0x0, 0x80, 0x0, 0x0, 0x6, 0x0, 0x0, 0x0, 0x68, 0x65, 0x6c, 0x6c, 0x6f, 0x21, 0x0, 0x0
        ];

        XpcData data = (XpcData) XpcSerialiser.Deserialise(wireBytes);

        Assert.AreSequenceEqual("hello!"u8.ToArray(), data.Data);
        AssertBytesEqual(wireBytes, XpcSerialiser.Serialise(data));
    }

    [TestMethod]
    public void XpcDate_RoundTrips() {
        // Nanoseconds chosen as an exact multiple of 100 so the conversion through
        // DateTime's 100ns ticks loses no precision on the way back out.
        byte[] wireBytes = [0x0, 0x70, 0x0, 0x0, 0xbc, 0xcc, 0x85, 0x3d, 0xfe, 0x9c, 0x97, 0x17];

        XpcDate date = (XpcDate) XpcSerialiser.Deserialise(wireBytes);

        Assert.AreEqual(XpcMessageType.Date, date.Type);
        AssertBytesEqual(wireBytes, XpcSerialiser.Serialise(date));
    }

    [TestMethod]
    public void XpcFd_RoundTrips() {
        byte[] wireBytes = [0x0, 0xb0, 0x0, 0x0, 0x2a, 0x0, 0x0, 0x0];

        XpcFd fd = (XpcFd) XpcSerialiser.Deserialise(wireBytes);

        Assert.AreEqual(XpcMessageType.Fd, fd.Type);
        Assert.AreEqual(42u, fd.Data);
        AssertBytesEqual(wireBytes, XpcSerialiser.Serialise(fd));
    }

    [TestMethod]
    public void XpcShmem_RoundTrips() {
        byte[] wireBytes = [0x0, 0xc0, 0x0, 0x0, 0x0, 0x10, 0x0, 0x0, 0x7, 0x0, 0x0, 0x0];

        XpcShmem shmem = (XpcShmem) XpcSerialiser.Deserialise(wireBytes);

        Assert.AreEqual(XpcMessageType.Shmem, shmem.Type);
        Assert.AreEqual(4096, shmem.Data);
        Assert.AreEqual(7, shmem.Reserved);
        AssertBytesEqual(wireBytes, XpcSerialiser.Serialise(shmem));
    }

    [TestMethod]
    public void XpcFileTransfer_Deserialise_DiscardsTransferIdMatchingUpstream() {
        // Captured from upstream: FileTransferType(transfer_size=0x1234, transfer_id=0x99)
        byte[] wireBytes = [
            0x0, 0xa0, 0x1, 0x0, 0x99, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0xf0, 0x0, 0x0,
            0x14, 0x0, 0x0, 0x0, 0x1, 0x0, 0x0, 0x0, 0x73, 0x0, 0x0, 0x0, 0x0, 0x40, 0x0, 0x0,
            0x34, 0x12, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0
        ];

        XpcFileTransfer fileTransfer = (XpcFileTransfer) XpcSerialiser.Deserialise(wireBytes);

        Assert.AreEqual(XpcMessageType.FileTransfer, fileTransfer.Type);
        Assert.AreEqual(0x1234ul, fileTransfer.TransferSize);
        // Upstream pymobiledevice3 never reads msg_id back out on decode: the receiving
        // code derives the HTTP/2 stream positionally instead. transfer_id stays 0.
        Assert.AreEqual(0ul, fileTransfer.TransferId);
    }

    [TestMethod]
    public void XpcFileTransfer_Serialise_IncludesTransferId() {
        byte[] expectedWireBytes = [
            0x0, 0xa0, 0x1, 0x0, 0x99, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0xf0, 0x0, 0x0,
            0x14, 0x0, 0x0, 0x0, 0x1, 0x0, 0x0, 0x0, 0x73, 0x0, 0x0, 0x0, 0x0, 0x40, 0x0, 0x0,
            0x34, 0x12, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0
        ];
        XpcFileTransfer fileTransfer = new(0x1234, 0x99);

        AssertBytesEqual(expectedWireBytes, XpcSerialiser.Serialise(fileTransfer));
    }
}

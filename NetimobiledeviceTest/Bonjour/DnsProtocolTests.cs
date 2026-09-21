using Netimobiledevice.Bonjour;
using System.Net;
using System.Text;

namespace NetimobiledeviceTest.Bonjour;

[TestClass]
public class DnsProtocolTests {
    [TestMethod]
    public void EncodeName_EncodesDnsLabels() {
        byte[] result =
            DnsProtocol.EncodeName(
                "_remoted._tcp.local.");

        Assert.AreSequenceEqual(
            new byte[]
            {
                8,
                (byte)'_',
                (byte)'r',
                (byte)'e',
                (byte)'m',
                (byte)'o',
                (byte)'t',
                (byte)'e',
                (byte)'d',
                4,
                (byte)'_',
                (byte)'t',
                (byte)'c',
                (byte)'p',
                5,
                (byte)'l',
                (byte)'o',
                (byte)'c',
                (byte)'a',
                (byte)'l',
                0
            }, result);
    }

    [TestMethod]
    public void EncodeName_RejectsLabelLongerThan63Bytes() {
        string name = new string('a', 64) + ".local";
        Assert.ThrowsExactly<ArgumentException>(() => DnsProtocol.EncodeName(name));
    }

    [TestMethod]
    public void DecodeName_DecodesNormalName() {
        byte[] encoded =
            DnsProtocol.EncodeName(
                "device.local.");

        (string? name, int offset) =
            DnsProtocol.DecodeName(
                encoded,
                0);

        Assert.AreEqual(
            "device.local.",
            name);

        Assert.AreEqual(
            encoded.Length,
            offset);
    }

    [TestMethod]
    public void DecodeName_DecodesCompressionPointer() {
        byte[] first =
            DnsProtocol.EncodeName(
                "device.local.");

        byte[] data =
            [
                .. first,
                .. new byte[]
                {
                    0xC0,
                    0x00
                },
            ];

        (string? name, int offset) =
            DnsProtocol.DecodeName(
                data,
                first.Length);

        Assert.AreEqual(
            "device.local.",
            name);

        Assert.AreEqual(
            first.Length + 2,
            offset);
    }

    [TestMethod]
    public void DecodeName_RejectsBadPointer() {
        byte[] data =
            [
                0xC0,
                0xFF
            ];

        Assert.ThrowsExactly<InvalidDataException>(
            () => DnsProtocol.DecodeName(data, 0));
    }

    [TestMethod]
    public void BuildQuery_BuildsPtrQuestion() {
        byte[] query =
            DnsProtocol.BuildQuery(
                "_remoted._tcp.local.",
                MdnsConstants.QTypePtr);

        Assert.HasCount(
            12 + DnsProtocol.EncodeName(
                    "_remoted._tcp.local.").Length +
                4,
            query);

        Assert.AreEqual(
            0,
            query[0]);

        Assert.AreEqual(
            1,
            query[5]);

        List<(string Name, ushort QType)> questions =
            DnsProtocol.ParseQuestions(query);

        Assert.HasCount(1, questions);

        Assert.AreEqual(
            "_remoted._tcp.local.",
            questions[0].Name);

        Assert.AreEqual(
            MdnsConstants.QTypePtr,
            questions[0].QType);
    }

    [TestMethod]
    public void BuildQuery_SetsUnicastBit() {
        byte[] query =
            DnsProtocol.BuildQuery(
                "test.local.",
                MdnsConstants.QTypeA,
                unicast: true);

        byte[] qname =
            DnsProtocol.EncodeName(
                "test.local.");

        int qclassOffset =
            12 + qname.Length + 2;

        ushort qclass =
            (ushort) (
                (query[qclassOffset] << 8) |
                query[qclassOffset + 1]);

        Assert.AreEqual(
            MdnsConstants.ClassIn |
            MdnsConstants.ClassQu,
            qclass);
    }

    private static readonly string[] expected = [
        "device=iPhone",
        "version=17"
    ];

    [TestMethod]
    public void EncodeTxt_EncodesKeyValuePairs() {
        byte[] result =
            DnsProtocol.EncodeTxt(
                new Dictionary<string, string> {
                    ["device"] = "iPhone",
                    ["version"] = "17"
                });

        List<string> text = DecodeTxt(result);

        Assert.AreSequenceEqual(expected, text, SequenceOrder.InAnyOrder);
    }

    [TestMethod]
    public void EncodeTxt_EncodesEmptyProperties() {
        byte[] result =
            DnsProtocol.EncodeTxt(
                new Dictionary<string, string>());

        Assert.AreSequenceEqual(
            new byte[] { 0 }, result);
    }

    [TestMethod]
    public void EncodeTxt_RejectsEntryLongerThan255Bytes() {
        Dictionary<string, string> properties =
            new Dictionary<string, string> {
                ["key"] = new string('x', 253)
            };

        Assert.ThrowsExactly<ArgumentException>(
            () => DnsProtocol.EncodeTxt(properties));
    }

    [TestMethod]
    public void EncodeSrv_ProducesValidSrvRecord() {
        byte[] rdata =
            DnsProtocol.EncodeSrv(
                0,
                0,
                62078,
                "device.local.");

        Assert.HasCount(
            6 + DnsProtocol.EncodeName(
                    "device.local.").Length,
            rdata);

        byte[] packet =
            DnsProtocol.BuildRecord(
                "_service._tcp.local.",
                MdnsConstants.QTypeSrv,
                rdata,
                120,
                true);

        int offset = 0;

        DnsRecord record =
            DnsProtocol.ParseRecord(
                packet,
                ref offset);

        Assert.AreEqual(
            MdnsConstants.QTypeSrv,
            record.Type);

        Assert.AreEqual(
            (ushort) 62078,
            record.Port);

        Assert.AreEqual(
            "device.local.",
            record.Target);
    }

    [TestMethod]
    public void ParseRecord_ParsesIpv4() {
        byte[] recordBytes =
            DnsProtocol.BuildRecord(
                "device.local.",
                MdnsConstants.QTypeA,
                IPAddress.Parse("192.168.1.50")
                    .GetAddressBytes(),
                120,
                true);

        int offset = 0;

        DnsRecord record =
            DnsProtocol.ParseRecord(
                recordBytes,
                ref offset);

        Assert.AreEqual(
            "192.168.1.50",
            record.Address);
    }

    [TestMethod]
    public void ParseRecord_ParsesIpv6() {
        IPAddress ip =
            IPAddress.Parse(
                "2001:db8::1234");

        byte[] recordBytes =
            DnsProtocol.BuildRecord(
                "device.local.",
                MdnsConstants.QTypeAaaa,
                ip.GetAddressBytes(),
                120,
                true);

        int offset = 0;

        DnsRecord record =
            DnsProtocol.ParseRecord(
                recordBytes,
                ref offset);

        Assert.AreEqual(
            "2001:db8::1234",
            record.Address);
    }

    [TestMethod]
    public void ParseRecord_ParsesTxt() {
        byte[] rdata =
            DnsProtocol.EncodeTxt(
                new Dictionary<string, string> {
                    ["foo"] = "bar",
                    ["empty"] = ""
                });

        byte[] packet =
            DnsProtocol.BuildRecord(
                "device.local.",
                MdnsConstants.QTypeTxt,
                rdata,
                120,
                true);

        int offset = 0;

        DnsRecord record =
            DnsProtocol.ParseRecord(
                packet,
                ref offset);

        Assert.IsNotNull(record.Txt);

        Assert.AreEqual(
            "bar",
            record.Txt["foo"]);

        Assert.AreEqual(
            "",
            record.Txt["empty"]);
    }

    [TestMethod]
    public void ParseMdnsMessage_ParsesMultipleRecords() {
        byte[] ptr =
            DnsProtocol.BuildRecord(
                "_demo._tcp.local.",
                MdnsConstants.QTypePtr,
                DnsProtocol.EncodeName(
                    "Device._demo._tcp.local."),
                120,
                false);

        byte[] a =
            DnsProtocol.BuildRecord(
                "device.local.",
                MdnsConstants.QTypeA,
                IPAddress.Parse(
                    "10.0.0.10").GetAddressBytes(),
                120,
                true);

        byte[] message =
            BuildMessage(
                [],
                [.. ptr, .. a]);

        List<DnsRecord> records =
            DnsProtocol.ParseMdnsMessage(message);

        Assert.HasCount(2, records);

        Assert.AreEqual(
            "Device._demo._tcp.local.",
            records[0].PtrdName);

        Assert.AreEqual(
            "10.0.0.10",
            records[1].Address);
    }

    [TestMethod]
    public void ParseQuestions_StopsOnTruncatedQuestion() {
        byte[] valid =
            DnsProtocol.BuildQuery(
                "_demo._tcp.local.",
                MdnsConstants.QTypePtr);

        byte[] truncated =
            valid[..^1];

        List<(string Name, ushort QType)> questions =
            DnsProtocol.ParseQuestions(truncated);

        Assert.IsEmpty(
            questions);
    }

    private static byte[] BuildMessage(
        byte[] questions,
        byte[] answers) {
        using MemoryStream stream = new MemoryStream();

        WriteUInt16(stream, 0);
        WriteUInt16(stream, 0x8400);
        WriteUInt16(stream, 0);
        WriteUInt16(
            stream,
            CountRecords(answers));

        WriteUInt16(stream, 0);
        WriteUInt16(stream, 0);

        stream.Write(questions);
        stream.Write(answers);

        return stream.ToArray();
    }

    private static ushort CountRecords(
        byte[] data) {
        // The tests only use two records and this helper is intentionally
        // kept local to the test fixture.
        int first = DnsProtocol.EncodeName(
            "_demo._tcp.local.").Length + 10;

        int second = DnsProtocol.EncodeName(
            "device.local.").Length + 10 + 4;

        _ = first;
        _ = second;

        return 2;
    }

    private static void WriteUInt16(
        Stream stream,
        ushort value) {
        stream.WriteByte((byte) (value >> 8));
        stream.WriteByte((byte) value);
    }

    private static List<string> DecodeTxt(
        byte[] data) {
        List<string> result = [];
        int offset = 0;

        while (offset < data.Length) {
            byte length = data[offset++];

            if (length == 0) {
                continue;
            }

            result.Add(
                Encoding.UTF8.GetString(
                    data,
                    offset,
                    length));

            offset += length;
        }

        return result;
    }
}

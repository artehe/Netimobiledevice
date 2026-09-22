using Netimobiledevice.Bonjour;
using System.Net;

namespace NetimobiledeviceTest.Bonjour;

internal static class FakeMdnsPacket {
    private static byte[] BuildResponse(params byte[][] records) {
        using (MemoryStream stream = new MemoryStream()) {
            // ID
            stream.WriteByte(0);
            stream.WriteByte(0);

            // Response + authoritative
            stream.WriteByte(0x84);
            stream.WriteByte(0x00);

            // Questions
            stream.WriteByte(0);
            stream.WriteByte(0);

            // Answers
            stream.WriteByte(0);
            stream.WriteByte((byte) records.Length);

            // Authority
            stream.WriteByte(0);
            stream.WriteByte(0);

            // Additional
            stream.WriteByte(0);
            stream.WriteByte(0);

            foreach (byte[] record in records) {
                stream.Write(record);
            }
            return stream.ToArray();
        }
    }

    public static MdnsPacket A(string hostname, string ip) {
        byte[] record = DnsProtocol.BuildRecord(
            hostname,
            MdnsConstants.QTypeA,
            IPAddress.Parse(ip).GetAddressBytes(),
            120,
            cacheFlush: true);

        return new MdnsPacket(
            BuildResponse(record),
            new IPEndPoint(
                IPAddress.Parse(ip),
                5353));
    }

    public static MdnsPacket Aaaa(string hostname, string ip) {
        IPAddress address = IPAddress.Parse(ip);
        if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6) {
            throw new ArgumentException("AAAA records require an IPv6 address.", nameof(ip));
        }
        byte[] record = DnsProtocol.BuildRecord(
            hostname,
            MdnsConstants.QTypeAaaa,
            address.GetAddressBytes(),
            120,
            cacheFlush: true);
        return new MdnsPacket(BuildResponse(record), new IPEndPoint(address, 5353));
    }

    public static MdnsPacket Ptr(string service, string instance) {
        byte[] record =
            DnsProtocol.BuildRecord(
                service,
                MdnsConstants.QTypePtr,
                DnsProtocol.EncodeName(instance),
                120,
                cacheFlush: false);

        return new MdnsPacket(
            BuildResponse(record),
            new IPEndPoint(
                IPAddress.Parse("192.168.1.100"),
                5353));
    }

    public static MdnsPacket Srv(string instance, string host, int port) {
        byte[] record =
            DnsProtocol.BuildRecord(
                instance,
                MdnsConstants.QTypeSrv,
                DnsProtocol.EncodeSrv(
                    0,
                    0,
                    (ushort) port,
                    host),
                120,
                cacheFlush: true);

        return new MdnsPacket(
            BuildResponse(record),
            new IPEndPoint(
                IPAddress.Parse("192.168.1.100"),
                5353));
    }

    public static MdnsPacket Txt(string instance, Dictionary<string, string> properties) {
        byte[] record =
            DnsProtocol.BuildRecord(
                instance,
                MdnsConstants.QTypeTxt,
                DnsProtocol.EncodeTxt(properties),
                120,
                cacheFlush: true);

        return new MdnsPacket(
            BuildResponse(record),
            new IPEndPoint(
                IPAddress.Parse("192.168.1.100"),
                5353));
    }
}

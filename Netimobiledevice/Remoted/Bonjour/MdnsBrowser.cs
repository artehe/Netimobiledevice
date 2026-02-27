using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Netimobiledevice.Remoted.Bonjour;

internal class MdnsBrowser {
    private const string MdnsMulticastV4 = "224.0.0.251";
    private const string MdnsMulticastV6 = "ff02::fb";
    private const int MdnsPort = 5353;

    private readonly UdpClient _clientV4;
    private readonly UdpClient _clientV6;
    private readonly NetworkInterface[] _interfaces;

    public MdnsBrowser() {
        _interfaces = [.. NetworkInterface.GetAllNetworkInterfaces()];
        _clientV4 = BindUdpV4();
        _clientV6 = BindUdpV6();
    }

    private static UdpClient BindUdpV4() {
        UdpClient client = new UdpClient(AddressFamily.InterNetwork);
        client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        client.Client.Bind(new IPEndPoint(IPAddress.Any, MdnsPort));
        client.JoinMulticastGroup(IPAddress.Parse(MdnsMulticastV4));
        return client;
    }

    private UdpClient BindUdpV6() {
        UdpClient client = new UdpClient(AddressFamily.InterNetworkV6);
        client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        client.Client.Bind(new IPEndPoint(IPAddress.IPv6Any, MdnsPort));

        for (int i = 0; i < _interfaces.Length; i++) {
            try { 
                client.JoinMulticastGroup(i, IPAddress.Parse(MdnsMulticastV6)); 
            }
            catch {
                // Catch any errors that might occurs and skip onto the next one
                continue;
            }
        }

        return client;
    }

    private void ParseMdnsMessage(
        byte[] data, 
        HashSet<string> ptrTargets,
        Dictionary<string, List<Service>> srvMap,
        Dictionary<string, Dictionary<string, string>> txtMap,
        Dictionary<string, List<Address>> hostAddrs
    ) {
        if (data.Length < 12) {
            return;
        }

        int qdCount = (data[4] << 8) | data[5];
        int anCount = (data[6] << 8) | data[7];
        int nsCount = (data[8] << 8) | data[9];
        int arCount = (data[10] << 8) | data[11];
        int offset = 12;

        for (int i = 0; i < qdCount; i++) {
            (string _, int newOffset) = DnsHelpers.DecodeName(data, offset);
            offset = newOffset + 4;
        }

        for (int i = 0; i < anCount + nsCount + arCount; i++) {
            offset = ParseRR(data, offset, ptrTargets, srvMap, txtMap, hostAddrs);
        }
    }

    private int ParseRR(
        byte[] data, 
        int offset, 
        HashSet<string> ptrTargets,
        Dictionary<string, List<Service>> srvMap,
        Dictionary<string, Dictionary<string, string>> txtMap,
        Dictionary<string, List<Address>> hostAddrs
    ) {
        (string? name, int newOffset) = DnsHelpers.DecodeName(data, offset);
        offset = newOffset;

        if (offset + 10 > data.Length) {
            return offset;
        }
        ushort rtype = (ushort) ((data[offset] << 8) | data[offset + 1]);
        ushort rclass = (ushort) ((data[offset + 2] << 8) | data[offset + 3]);
        ushort rdlen = (ushort) ((data[offset + 8] << 8) | data[offset + 9]);
        offset += 10;

        if (offset + rdlen > data.Length) {
            return offset;
        }
        byte[] rdata = new byte[rdlen];
        Array.Copy(data, offset, rdata, 0, rdlen);
        offset += rdlen;

        if (rtype == DnsHelpers.QTYPE_PTR) {
            (string? target, int _) = DnsHelpers.DecodeName(rdata, 0);
            ptrTargets.Add(target);
        }
        else if (rtype == DnsHelpers.QTYPE_SRV && rdlen >= 6) {
            ushort port = (ushort) ((rdata[4] << 8) | rdata[5]);
            (string? target, int _) = DnsHelpers.DecodeName(rdata, 6);
            if (!srvMap.ContainsKey(name)) {
                srvMap[name] = [];
            }
            srvMap[name].Add(new(target, port));
        }
        else if (rtype == DnsHelpers.QTYPE_TXT) {
            var dict = new Dictionary<string, string>();
            int idx = 0;
            while (idx < rdlen) {
                int len = rdata[idx++];
                if (idx + len > rdlen) {
                    break;
                }
                string txt = Encoding.UTF8.GetString(rdata, idx, len);
                idx += len;
                string[] parts = txt.Split('=', 2);
                dict[parts[0]] = parts.Length == 2 ? parts[1] : "";
            }
            txtMap[name] = dict;
        }
        else if ((rtype == DnsHelpers.QTYPE_A && rdlen == 4) ||
                 (rtype == DnsHelpers.QTYPE_AAAA && rdlen == 16)) {
            IPAddress ip = new IPAddress(rdata);
            string iface = PickInterfaceForIp(ip);
            if (iface == null) {
                return offset;
            }

            if (!hostAddrs.ContainsKey(name)) {
                hostAddrs[name] = [];
            }
            List<Address> existing = hostAddrs[name];
            if (!existing.Exists(a => a.Ip == ip.ToString())) {
                existing.Add(new Address(ip.ToString(), iface));
            }
        }

        return offset;
    }

    private string PickInterfaceForIp(IPAddress ip) {
        foreach (NetworkInterface ni in _interfaces) {
            IPInterfaceProperties props = ni.GetIPProperties();
            foreach (UnicastIPAddressInformation uni in props.UnicastAddresses) {
                if (uni.Address.AddressFamily != ip.AddressFamily) {
                    continue;
                }
                if (ip.AddressFamily == AddressFamily.InterNetwork) {
                    uint ipInt = BitConverter.ToUInt32(ip.GetAddressBytes(), 0);
                    uint uniInt = BitConverter.ToUInt32(uni.Address.GetAddressBytes(), 0);
                    uint maskInt = BitConverter.ToUInt32(uni.IPv4Mask.GetAddressBytes(), 0);
                    if ((ipInt & maskInt) == (uniInt & maskInt)) {
                        return ni.Name;
                    }
                }
                else {
                    if (ip.IsIPv6LinkLocal) {
                        return ni.Name;
                    }
                }
            }
        }
        return string.Empty;
    }

    /// <summary>
    /// Discover a DNS-SD/mDNS service type (e.g. "_remoted._tcp.local.") on the local network.
    /// </summary>
    /// <param name="serviceType"></param>
    /// <param name="timeout"></param>
    /// <returns>List of ServiceInstance with Address(ip, interface) entries.</returns>
    public async Task<List<ServiceInstance>> BrowseService(string serviceType, int timeout) {
        if (!serviceType.EndsWith('.')) {
            serviceType += ".";
        }

        byte[] query = DnsHelpers.BuildQuery(serviceType, DnsHelpers.QTYPE_PTR);
        await SendQuery(query).ConfigureAwait(false);

        HashSet<string> ptrTargets = [];
        Dictionary<string, List<Service>> srvMap = [];
        Dictionary<string, Dictionary<string, string>> txtMap = [];
        Dictionary<string, List<Address>> hostAddrs = [];

        DateTime endTime = DateTime.UtcNow.AddSeconds(timeout);
        while (DateTime.UtcNow < endTime) {
            List<Task<UdpReceiveResult>> tasks = [];
            if (_clientV4.Available > 0) {
                tasks.Add(_clientV4.ReceiveAsync());
            }
            if (_clientV6.Available > 0) {
                tasks.Add(_clientV6.ReceiveAsync());
            }
            if (tasks.Count == 0) {
                await Task.Delay(50);
                continue;
            }

            Task<UdpReceiveResult> completed = await Task.WhenAny(tasks).ConfigureAwait(false);
            UdpReceiveResult result = completed.Result;
            byte[] data = result.Buffer;

            try { 
                ParseMdnsMessage(data, ptrTargets, srvMap, txtMap, hostAddrs);
            }
            catch {
                // Ignore any exception that happen.
            }
        }

        _clientV4.DropMulticastGroup(IPAddress.Parse(MdnsMulticastV4));
        _clientV6.DropMulticastGroup(IPAddress.Parse(MdnsMulticastV6));
        _clientV4.Close();
        _clientV6.Close();

        List<ServiceInstance> services = [];
        foreach (string inst in ptrTargets) {
            if (!srvMap.ContainsKey(inst)) {
                continue;
            }
            foreach (Service srv in srvMap[inst]) {
                ServiceInstance si = new(inst) {
                    Host = srv.Target.TrimEnd('.'),
                    Port = srv.Port,
                    Addresses = hostAddrs.TryGetValue(srv.Target, out List<Address>? value1) ? value1 : [],
                    Properties = txtMap.TryGetValue(inst, out Dictionary<string, string>? value) ? value : []
                };
                services.Add(si);
            }
        }

        return services;
    }

    public async Task SendQuery(byte[] query) {
        await _clientV4.SendAsync(query, query.Length, new IPEndPoint(IPAddress.Parse(MdnsMulticastV4), MdnsPort));
        for (int i = 0; i < _interfaces.Length; i++) {
            try {
                await _clientV6.SendAsync(query, query.Length, new IPEndPoint(IPAddress.Parse(MdnsMulticastV6), MdnsPort));
            }
            catch {
                // Catch any errors that might occurs and skip onto the next one
                continue;
            }
        }
    }
}

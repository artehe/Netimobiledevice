using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Bonjour;

public sealed class MdnsResponder : IAsyncDisposable {
    public const uint DefaultTtl = 120;
    public const uint HostTtl = 120;

    private readonly IMdnsSocketFactory _socketFactory;

    private readonly string _serviceType;
    private readonly string _instanceName;
    private readonly int _port;
    private readonly Dictionary<string, string> _properties;
    private readonly string _hostname;
    private readonly List<(AddressFamily Family, string Ip)> _addresses;
    private readonly List<string> _ipv4SendAddresses;

    private IMdnsSocketSet? _sockets;
    private CancellationTokenSource? _serveCts;
    private Task? _serveTask;

    public MdnsResponder(
        IMdnsSocketFactory socketFactory,
        string serviceType,
        string instanceName,
        int port,
        IReadOnlyDictionary<string, string> properties,
        string? hostname = null,
        IReadOnlyList<(AddressFamily Family, string Ip)>? addresses = null) {
        _socketFactory = socketFactory;

        if (!serviceType.EndsWith('.')) {
            serviceType += ".";
        }

        _serviceType = serviceType;
        _instanceName = instanceName;
        _port = port;
        _properties = new Dictionary<string, string>(properties);

        InstanceFqdn = $"{instanceName}.{serviceType}";

        _hostname =
            hostname ??
            $"{instanceName.Split('.')[0]}.local.";

        if (!_hostname.EndsWith('.')) {
            _hostname += ".";
        }

        _addresses = addresses?.ToList() ?? LocalAddresses();
        _ipv4SendAddresses = [.. _addresses
            .Where(x => x.Family == AddressFamily.InterNetwork)
            .Select(x => x.Ip)
        ];
    }

    public string ServiceType => _serviceType;

    public string InstanceFqdn { get; }

    public string Hostname => _hostname;

    public int Port => _port;

    public IReadOnlyList<(AddressFamily Family, string Ip)> Addresses => _addresses;

    private async Task AnnounceAsync(
       uint? ttl,
       CancellationToken cancellationToken
    ) {
        IMdnsSocketSet? sockets = _sockets;
        if (sockets is null) {
            return;
        }

        (List<byte[]> answers, List<byte[]> additionals) = AllRecords(ttl);
        byte[] packet = BuildMessage(answers, additionals);
        await sockets.SendToAllAsync(packet, _ipv4SendAddresses, cancellationToken);
    }

    private static List<(AddressFamily Family, string Ip)> LocalAddresses() {
        List<(AddressFamily Family, string Ip)> result = [];
        foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces()) {
            foreach (UnicastIPAddressInformation address in adapter
                         .GetIPProperties()
                         .UnicastAddresses) {
                IPAddress ip = address.Address;

                if (ip.AddressFamily ==
                    AddressFamily.InterNetwork) {
                    if (ip.ToString().StartsWith("127.", StringComparison.InvariantCulture) || ip.Equals(IPAddress.Any)) {
                        continue;
                    }

                    result.Add((AddressFamily.InterNetwork, ip.ToString()));
                }
                else if (ip.AddressFamily == AddressFamily.InterNetworkV6) {
                    string text = ip.ToString();
                    if (text == "::1" ||
                        text == "::" ||
                        text.StartsWith("fe80:", StringComparison.OrdinalIgnoreCase)
                    ) {
                        continue;
                    }

                    result.Add((AddressFamily.InterNetworkV6, text));
                }
            }
        }

        return result;
    }

    private void ProcessPacket(MdnsPacket packet, CancellationToken cancellationToken) {
        byte[] data = packet.Data;
        if (data.Length < 12) { return; }

        // DNS header: 
        // 
        // 0-1 transaction ID 
        // 2-3 flags         
        ushort flags = (ushort) ((data[2] << 8) | data[3]);

        // QR=1 means this is a response. 
        // We only want to answer queries.
        if ((flags & 0x8000) != 0) {
            return;
        }

        List<(string Name, ushort QType)> questions;
        try {
            questions = DnsProtocol.ParseQuestions(data);
        }
        catch (Exception) {
            // A malformed packet should not kill the responder.
            return;
        }

        if (!questions.Any(question => Matches(question.Name, question.QType))) {
            return;
        }

        // Don't await here. ProcessPacket is intentionally synchronous so 
        // one slow multicast send cannot stop reception of subsequent queries.
        _ = AnnounceAsync(null, cancellationToken);
    }

    private async Task ServeAsync(CancellationToken cancellationToken) {
        IMdnsSocketSet sockets = _sockets ?? throw new InvalidOperationException("mDNS sockets have not been opened.");
        try {
            await foreach (MdnsPacket packet in sockets.ReceiveAsync(cancellationToken)) {
                ProcessPacket(packet, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            // Normal shutdown.
        }
        catch (ObjectDisposedException) {
            // Socket set was disposed while receiving.
        }
    }

    private static void WriteUInt16(
        Stream stream,
        ushort value) {
        stream.WriteByte((byte) (value >> 8));
        stream.WriteByte((byte) value);
    }

    public IReadOnlyList<byte[]> AddressRecords() {
        List<byte[]> records = [];

        foreach ((AddressFamily family, string? ip) in _addresses) {
            IPAddress address = IPAddress.Parse(ip);

            records.Add(
                DnsProtocol.BuildRecord(
                    _hostname,
                    family == AddressFamily.InterNetwork
                        ? MdnsConstants.QTypeA
                        : MdnsConstants.QTypeAaaa,
                    address.GetAddressBytes(),
                    HostTtl,
                    cacheFlush: true));
        }

        return records;
    }

    public (List<byte[]> Answers, List<byte[]> Additionals) AllRecords(uint? ttl = null) {
        uint actualTtl = ttl ?? DefaultTtl;

        byte[] ptr =
            DnsProtocol.BuildRecord(
                _serviceType,
                MdnsConstants.QTypePtr,
                DnsProtocol.EncodeName(InstanceFqdn),
                actualTtl,
                cacheFlush: false);

        byte[] srv =
            DnsProtocol.BuildRecord(
                InstanceFqdn,
                MdnsConstants.QTypeSrv,
                DnsProtocol.EncodeSrv(
                    0,
                    0,
                    checked((ushort) _port),
                    _hostname),
                actualTtl,
                cacheFlush: true);

        byte[] txt =
            DnsProtocol.BuildRecord(
                InstanceFqdn,
                MdnsConstants.QTypeTxt,
                DnsProtocol.EncodeTxt(_properties),
                actualTtl,
                cacheFlush: true);

        List<byte[]> answers =
        [
            ptr,
            srv,
            txt
        ];

        List<byte[]> additionals =
            actualTtl == 0
                ? []
                : [.. AddressRecords()];

        return (answers, additionals);
    }

    public static byte[] BuildMessage(IReadOnlyList<byte[]> answers, IReadOnlyList<byte[]> additionals) {
        using (MemoryStream stream = new MemoryStream()) {
            WriteUInt16(stream, 0);
            WriteUInt16(stream, 0x8400);
            WriteUInt16(stream, 0);
            WriteUInt16(stream, checked((ushort) answers.Count));
            WriteUInt16(stream, 0);
            WriteUInt16(stream, checked((ushort) additionals.Count));

            foreach (byte[] answer in answers) {
                stream.Write(answer);
            }

            foreach (byte[] additional in additionals) {
                stream.Write(additional);
            }

            return stream.ToArray();
        }
    }

    public bool Matches(
        string name,
        ushort qType) {
        if (!name.EndsWith('.')) {
            name += ".";
        }

        if (name == _serviceType &&
            (qType == MdnsConstants.QTypePtr ||
             qType == 255)) {
            return true;
        }

        if (name == InstanceFqdn &&
            (qType == MdnsConstants.QTypeSrv ||
             qType == MdnsConstants.QTypeTxt ||
             qType == 255)) {
            return true;
        }

        return name == _hostname &&
               (qType == MdnsConstants.QTypeA ||
                qType == MdnsConstants.QTypeAaaa ||
                qType == 255);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default) {
        if (_sockets is not null) {
            throw new InvalidOperationException("Responder is already running.");
        }

        IMdnsSocketSet sockets = _socketFactory.Open();
        _sockets = sockets;
        _serveCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _serveTask = ServeAsync(_serveCts.Token);
        try {
            for (int i = 0; i < 2; i++) {
                await AnnounceAsync(null, cancellationToken);
                if (i == 0) {
                    await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
                }
            }
        }
        catch {
            // If startup fails, stop the receive task and dispose the 
            // socket set so StartAsync doesn't leave a half-running 
            // responder behind.
            _serveCts.Cancel();
            try {
                await _serveTask;
            }
            catch (OperationCanceledException) { }

            _sockets.Dispose();
            _sockets = null;

            _serveCts.Dispose();
            _serveCts = null;
            _serveTask = null;

            throw;
        }
    }

    public async ValueTask DisposeAsync() {
        if (_sockets is null) {
            return;
        }

        _serveCts?.Cancel();

        if (_serveTask is not null) {
            try {
                await _serveTask;
            }
            catch (OperationCanceledException) {
            }
        }

        try {
            await AnnounceAsync(0, CancellationToken.None);
        }
        catch {
            // Goodbye packets are best effort.
        }

        _sockets.Dispose();
        _sockets = null;

        _serveCts?.Dispose();
        _serveCts = null;
        _serveTask = null;
    }
}

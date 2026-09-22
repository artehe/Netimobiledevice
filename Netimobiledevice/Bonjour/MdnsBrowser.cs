using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Bonjour;

/// <summary>
/// mDNS browser returning data classes with per-address interface names.
/// Works for any DNS-SD type, e.g. "_remoted._tcp.local."
/// </summary>
public sealed class MdnsBrowser(IMdnsSocketFactory socketFactory, IMdnsInterfaceResolver interfaceResolver) {
    private readonly IMdnsInterfaceResolver _interfaceResolver = interfaceResolver;
    private readonly IMdnsSocketFactory _socketFactory = socketFactory;

    private static void AddAddress(DnsRecord rr, EndPoint packetEndPoint, IMdnsInterfaceResolver resolver, Dictionary<string, List<Address>> hostAddresses) {
        if (rr.Address is null) {
            return;
        }

        AddressFamily family = rr.Type switch {
            MdnsConstants.QTypeA =>
                AddressFamily.InterNetwork,

            MdnsConstants.QTypeAaaa =>
                AddressFamily.InterNetworkV6,

            _ => throw new ArgumentException(
                $"Record is not an address record: {rr.Type}")
        };

        long? scopeId = null;
        if (packetEndPoint is IPEndPoint ipEndPoint &&
            family == AddressFamily.InterNetworkV6) {
            scopeId = ipEndPoint.Address.ScopeId;
        }

        string? iface = resolver.PickInterface(rr.Address, family, scopeId);
        if (iface is null) {
            return;
        }

        if (!hostAddresses.TryGetValue(rr.Name, out List<Address>? addresses)) {
            addresses = [];
            hostAddresses[rr.Name] = addresses;
        }

        if (addresses.Any(a => a.Ip == rr.Address)) {
            return;
        }

        addresses.Add(
            new Address(
                rr.Address,
                iface));
    }

    private static List<ServiceInstance> AssembleResults(
        HashSet<string> ptrTargets,
        Dictionary<string, List<DnsRecord>> srvMap,
        Dictionary<string, Dictionary<string, string>> txtMap,
        Dictionary<string, List<Address>> hostAddresses) {
        List<ServiceInstance> results = [];

        foreach (string? instance in ptrTargets.OrderBy(x => x)) {
            if (!srvMap.TryGetValue(
                    instance,
                    out List<DnsRecord>? srvEntries)) {
                continue;
            }

            txtMap.TryGetValue(
                instance,
                out Dictionary<string, string>? properties);

            foreach (DnsRecord srv in srvEntries) {
                if (srv.Port is null) {
                    continue;
                }

                string? target = srv.Target;
                string? host =
                    target is not null &&
                    target.EndsWith('.')
                        ? target[..^1]
                        : target;

                if (string.IsNullOrEmpty(host)) {
                    host = null;
                }

                List<Address> addresses =
                    target is not null &&
                    hostAddresses.TryGetValue(
                        target,
                        out List<Address>? found)
                        ? found
                        : [];

                results.Add(
                    new ServiceInstance {
                        Instance = instance,
                        Host = host,
                        Port = srv.Port.Value,
                        Addresses = addresses,
                        Properties =
                            properties ??
                            []
                    });
            }
        }

        return results;
    }

    private void ProcessPacket(
        MdnsPacket packet,
        string serviceType,
        HashSet<string> ptrTargets,
        Dictionary<string, List<DnsRecord>> srvMap,
        Dictionary<string, Dictionary<string, string>> txtMap,
        Dictionary<string, List<Address>> hostAddresses
    ) {
        foreach (DnsRecord rr in DnsProtocol.ParseMdnsMessage(packet.Data)) {
            switch (rr.Type) {
                case MdnsConstants.QTypePtr
                    when rr.Name == serviceType:

                    if (rr.PtrdName is not null) {
                        ptrTargets.Add(rr.PtrdName);
                    }

                    break;

                case MdnsConstants.QTypeSrv:

                    if (!srvMap.TryGetValue(rr.Name, out List<DnsRecord>? srvList)) {
                        srvList = [];
                        srvMap[rr.Name] = srvList;
                    }

                    srvList.Add(rr);
                    break;

                case MdnsConstants.QTypeTxt:

                    txtMap[rr.Name] =
                        rr.Txt ?? [];

                    break;

                case MdnsConstants.QTypeA or MdnsConstants.QTypeAaaa when rr.Address is not null:
                    AddAddress(
                        rr,
                        packet.RemoteEndPoint,
                        _interfaceResolver,
                        hostAddresses);

                    break;
            }
        }
    }

    public Task<List<ServiceInstance>> BrowseRemotedAsync(
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default) =>
        BrowseServiceAsync(
            MdnsConstants.RemotedServiceName,
            timeout,
            cancellationToken);

    public Task<List<ServiceInstance>> BrowseRemotepairingAsync(
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default) =>
        BrowseServiceAsync(
            MdnsConstants.RemotePairingServiceName,
            timeout,
            cancellationToken);

    public Task<List<ServiceInstance>>
        BrowseRemotepairingManualPairingAsync(
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default) =>
        BrowseServiceAsync(
            MdnsConstants.RemotePairingManualPairingServiceName,
            timeout,
            cancellationToken);

    public Task<List<ServiceInstance>> BrowseMobdev2Async(
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    ) => BrowseServiceAsync(
        MdnsConstants.Mobdev2ServiceName,
        timeout,
        cancellationToken
    );

    public async Task<List<ServiceInstance>> BrowseServiceAsync(
        string serviceType,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    ) {
        if (!serviceType.EndsWith('.')) {
            serviceType += ".";
        }

        timeout ??= TimeSpan.FromSeconds(4);

        using (IMdnsSocketSet sockets = _socketFactory.Open()) {
            HashSet<string> ptrTargets = new HashSet<string>(StringComparer.Ordinal);
            Dictionary<string, List<DnsRecord>> srvMap = new Dictionary<string, List<DnsRecord>>(StringComparer.Ordinal);
            Dictionary<string, Dictionary<string, string>> txtMap = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            Dictionary<string, List<Address>> hostAddresses = new Dictionary<string, List<Address>>(StringComparer.Ordinal);
            byte[] query = DnsProtocol.BuildQuery(serviceType, MdnsConstants.QTypePtr);

            await sockets.SendQueryAsync(query, cancellationToken);

            using (CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)) {
                timeoutCts.CancelAfter(timeout.Value);

                try {
                    await foreach (MdnsPacket packet in sockets.ReceiveAsync(timeoutCts.Token)) {
                        ProcessPacket(
                            packet,
                            serviceType,
                            ptrTargets,
                            srvMap,
                            txtMap,
                            hostAddresses);
                    }
                }
                catch (OperationCanceledException) {
                    if (cancellationToken.IsCancellationRequested) {
                        throw;
                    }
                }

                return AssembleResults(
                    ptrTargets,
                    srvMap,
                    txtMap,
                    hostAddresses);
            }
        }
    }
}

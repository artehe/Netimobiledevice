using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Netimobiledevice.Bonjour;

/// <summary>
/// mDNS browser returning data classes with per-address interface names.
/// Works for any DNS-SD type, e.g. "_remoted._tcp.local."
/// </summary>
public static class MdnsBrowser {
    private static List<ServiceInstance> AssembleResults(
        HashSet<string> ptrTargets,
        Dictionary<string, List<DnsRecord>> srvMap,
        Dictionary<string, Dictionary<string, string>> txtMap,
        Dictionary<string, List<Address>> hostAddresses) {
        var results = new List<ServiceInstance>();

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

    private static void ProcessPacket(
        byte[] data,
        EndPoint remote,
        string serviceType,
        MdnsNetworkAdapters adapters,
        HashSet<string> ptrTargets,
        Dictionary<string, List<DnsRecord>> srvMap,
        Dictionary<string, Dictionary<string, string>> txtMap,
        Dictionary<string, List<Address>> hostAddresses) {
        foreach (DnsRecord rr in DnsProtocol.ParseMdnsMessage(data)) {
            if (rr.Type == MdnsConstants.QTypePtr &&
                string.Equals(
                    rr.Name,
                    serviceType,
                    StringComparison.Ordinal)) {
                if (rr.PtrdName is not null) {
                    ptrTargets.Add(rr.PtrdName);
                }
            }
            else if (rr.Type == MdnsConstants.QTypeSrv) {
                if (!srvMap.TryGetValue(
                        rr.Name,
                        out List<DnsRecord>? list)) {
                    list = [];
                    srvMap[rr.Name] = list;
                }

                list.Add(rr);
            }
            else if (rr.Type == MdnsConstants.QTypeTxt) {
                txtMap[rr.Name] =
                    rr.Txt ?? [];
            }
            else if (
                (rr.Type == MdnsConstants.QTypeA ||
                 rr.Type == MdnsConstants.QTypeAaaa) &&
                rr.Address is not null) {
                AddressFamily family =
                    rr.Type == MdnsConstants.QTypeA
                        ? AddressFamily.InterNetwork
                        : AddressFamily.InterNetworkV6;

                int? scopeId =
                    remote is IPEndPoint ep &&
                    ep.AddressFamily ==
                    AddressFamily.InterNetworkV6
                        ? (int?) ep.Address.ScopeId
                        : null;

                string? iface =
                    adapters.PickInterface(
                        rr.Address,
                        family,
                        scopeId);

                if (iface is null) {
                    continue;
                }

                if (!hostAddresses.TryGetValue(
                        rr.Name,
                        out List<Address>? addresses)) {
                    addresses = [];
                    hostAddresses[rr.Name] = addresses;
                }

                if (!addresses.Any(
                        a => a.Ip == rr.Address)) {
                    addresses.Add(
                        new Address(
                            rr.Address,
                            iface));
                }
            }
        }
    }

    public static Task<List<ServiceInstance>> BrowseRemotedAsync(
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default) =>
        BrowseServiceAsync(
            MdnsConstants.RemotedServiceName,
            timeout,
            cancellationToken);

    public static Task<List<ServiceInstance>> BrowseRemotepairingAsync(
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default) =>
        BrowseServiceAsync(
            MdnsConstants.RemotePairingServiceName,
            timeout,
            cancellationToken);

    public static Task<List<ServiceInstance>>
        BrowseRemotepairingManualPairingAsync(
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default) =>
        BrowseServiceAsync(
            MdnsConstants.RemotePairingManualPairingServiceName,
            timeout,
            cancellationToken);

    public static Task<List<ServiceInstance>> BrowseMobdev2Async(
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default) =>
        BrowseServiceAsync(
            MdnsConstants.Mobdev2ServiceName,
            timeout,
            cancellationToken);

    public static async Task<List<ServiceInstance>> BrowseServiceAsync(
        string serviceType,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default) {
        if (!serviceType.EndsWith('.')) {
            serviceType += ".";
        }

        timeout ??= TimeSpan.FromSeconds(4);

        using var sockets = MdnsSocketSet.Open();

        var adapters = new MdnsNetworkAdapters();

        var ptrTargets = new HashSet<string>(
            StringComparer.Ordinal);

        var srvMap =
            new Dictionary<string, List<DnsRecord>>(
                StringComparer.Ordinal);

        var txtMap =
            new Dictionary<string, Dictionary<string, string>>(
                StringComparer.Ordinal);

        var hostAddresses =
            new Dictionary<string, List<Address>>(
                StringComparer.Ordinal);

        await sockets.SendQueryAsync(
            DnsProtocol.BuildQuery(
                serviceType,
                MdnsConstants.QTypePtr),
            cancellationToken);

        using var timeoutCts =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);

        timeoutCts.CancelAfter(timeout.Value);

        var channel = Channel.CreateUnbounded<MdnsBrowserRecievedPacket>();

        Task[] receiveTasks = [.. sockets.Sockets
            .Select(socket =>
                Task.Run(async () => {
                    while (!timeoutCts.IsCancellationRequested) {
                        try {
                            (byte[] Data, EndPoint Remote) = await MdnsSocketSet.ReceiveAsync(socket, timeoutCts.Token);

                            await channel.Writer.WriteAsync(
                                new MdnsBrowserRecievedPacket(
                                    Data,
                                    Remote
                                ),
                                timeoutCts.Token);
                        }
                        catch (OperationCanceledException) {
                            break;
                        }
                        catch (ObjectDisposedException) {
                            break;
                        }
                    }
                },
                timeoutCts.Token)
            )];


        try {
            await foreach (MdnsBrowserRecievedPacket packet in channel.Reader.ReadAllAsync(timeoutCts.Token)) {
                ProcessPacket(
                    packet.Data,
                    packet.Remote,
                    serviceType,
                    adapters,
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
        finally {
            timeoutCts.Cancel();

            try {
                await Task.WhenAll(receiveTasks);
            }
            catch (OperationCanceledException) {
            }
        }

        return AssembleResults(
            ptrTargets,
            srvMap,
            txtMap,
            hostAddresses);
    }
}

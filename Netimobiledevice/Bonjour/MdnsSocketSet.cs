using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Bonjour;

internal sealed class MdnsSocketSet : IDisposable {
    private readonly List<Socket> _sockets = [];

    public IReadOnlyList<Socket> Sockets => _sockets;

    public static MdnsSocketSet Open() {
        MdnsSocketSet result = new MdnsSocketSet();

        try {
            result.OpenIpv4();
            result.OpenIpv6();
        }
        catch {
            result.Dispose();
            throw;
        }

        if (result._sockets.Count == 0) {
            throw new InvalidOperationException(
                "Failed to open mDNS sockets (UDP/5353)");
        }

        return result;
    }

    private void OpenIpv4() {
        try {
            Socket socket = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Dgram,
                ProtocolType.Udp);

            socket.SetSocketOption(
                SocketOptionLevel.Socket,
                SocketOptionName.ReuseAddress,
                true);

            socket.Bind(
                new IPEndPoint(
                    IPAddress.Any,
                    MdnsConstants.MdnsPort));

            try {
                socket.SetSocketOption(
                    SocketOptionLevel.IP,
                    SocketOptionName.AddMembership,
                    new MulticastOption(
                        IPAddress.Parse(
                            MdnsConstants.MdnsMulticastV4),
                        IPAddress.Any));
            }
            catch (SocketException) {
                // Match the Python implementation: continue even if
                // multicast membership fails.
            }

            _sockets.Add(socket);
        }
        catch (SocketException) {
            // IPv4 may not be available.
        }
    }

    private void OpenIpv6() {
        try {
            Socket socket = new Socket(
                AddressFamily.InterNetworkV6,
                SocketType.Dgram,
                ProtocolType.Udp);

            socket.SetSocketOption(
                SocketOptionLevel.Socket,
                SocketOptionName.ReuseAddress,
                true);

            socket.Bind(
                new IPEndPoint(
                    IPAddress.IPv6Any,
                    MdnsConstants.MdnsPort));

            IPAddress multicastAddress =
                IPAddress.Parse(
                    MdnsConstants.MdnsMulticastV6);

            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces()) {
                foreach (int address in ni.GetIPProperties()
                             .GetIPv6Properties() is { } ipv6Properties
                             ? [ipv6Properties.Index]
                             : Array.Empty<int>()) {
                    try {
                        socket.SetSocketOption(
                            SocketOptionLevel.IPv6,
                            SocketOptionName.AddMembership,
                            new IPv6MulticastOption(
                                multicastAddress,
                                address));
                    }
                    catch (SocketException) {
                        // Ignore interfaces that cannot join.
                    }
                }
            }

            _sockets.Add(socket);
        }
        catch (SocketException) {
            // IPv6 may not be available.
        }
    }

    public async Task SendQueryAsync(
        byte[] packet,
        CancellationToken cancellationToken = default) {
        foreach (Socket socket in _sockets) {
            if (socket.AddressFamily ==
                AddressFamily.InterNetwork) {
                await socket.SendToAsync(
                    packet,
                    SocketFlags.None,
                    new IPEndPoint(
                        IPAddress.Parse(
                            MdnsConstants.MdnsMulticastV4),
                        MdnsConstants.MdnsPort),
                    cancellationToken);
            }
            else {
                foreach (int index in GetInterfaceIndexes()) {
                    IPEndPoint endpoint = new IPEndPoint(
                        IPAddress.Parse(
                            MdnsConstants.MdnsMulticastV6),
                        MdnsConstants.MdnsPort) {
                        // ScopeId is part of IPv6 multicast routing.
                    };

                    endpoint.Address =
                        new IPAddress(
                            endpoint.Address.GetAddressBytes(),
                            index);

                    try {
                        await socket.SendToAsync(
                            packet,
                            SocketFlags.None,
                            endpoint,
                            cancellationToken);
                    }
                    catch (SocketException) {
                        // Match Python's best-effort multicast behavior.
                    }
                }
            }
        }
    }

    public async Task SendToAllAsync(
        byte[] packet,
        IReadOnlyList<string> ipv4Addresses,
        CancellationToken cancellationToken = default) {
        foreach (Socket socket in _sockets) {
            try {
                if (socket.AddressFamily ==
                    AddressFamily.InterNetwork) {
                    bool sent = false;

                    foreach (string address in ipv4Addresses) {
                        try {
                            socket.SetSocketOption(
                                SocketOptionLevel.IP,
                                SocketOptionName.MulticastInterface,
                                IPAddress.Parse(address)
                                    .GetAddressBytes());

                            await socket.SendToAsync(
                                packet,
                                SocketFlags.None,
                                new IPEndPoint(
                                    IPAddress.Parse(
                                        MdnsConstants.MdnsMulticastV4),
                                    MdnsConstants.MdnsPort),
                                cancellationToken);

                            sent = true;
                        }
                        catch (SocketException) {
                        }
                    }

                    if (!sent) {
                        await socket.SendToAsync(
                            packet,
                            SocketFlags.None,
                            new IPEndPoint(
                                IPAddress.Parse(
                                    MdnsConstants.MdnsMulticastV4),
                                MdnsConstants.MdnsPort),
                            cancellationToken);
                    }
                }
                else {
                    foreach (int index in GetInterfaceIndexes()) {
                        try {
                            IPAddress address =
                                new IPAddress(
                                    IPAddress.Parse(
                                        MdnsConstants.MdnsMulticastV6)
                                    .GetAddressBytes(),
                                    index);

                            await socket.SendToAsync(
                                packet,
                                SocketFlags.None,
                                new IPEndPoint(
                                    address,
                                    MdnsConstants.MdnsPort),
                                cancellationToken);
                        }
                        catch (SocketException) {
                        }
                    }
                }
            }
            catch (SocketException) {
            }
        }
    }

    public static IEnumerable<int> GetInterfaceIndexes() {
        foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces()) {
            IPv6InterfaceProperties? properties = ni.GetIPProperties()
                .GetIPv6Properties();

            if (properties is not null) {
                yield return properties.Index;
            }
        }
    }

    public static async Task<(byte[] Data, EndPoint Remote)> ReceiveAsync(
        Socket socket,
        CancellationToken cancellationToken) {
        byte[] buffer = new byte[65535];

        EndPoint endpoint =
            socket.AddressFamily ==
            AddressFamily.InterNetwork
                ? new IPEndPoint(IPAddress.Any, 0)
                : new IPEndPoint(IPAddress.IPv6Any, 0);

        SocketReceiveFromResult result = await socket.ReceiveFromAsync(
            buffer,
            SocketFlags.None,
            endpoint,
            cancellationToken);

        return (
            buffer[..result.ReceivedBytes],
            result.RemoteEndPoint);
    }

    public void Dispose() {
        foreach (Socket socket in _sockets) {
            try {
                socket.Dispose();
            }
            catch {
            }
        }

        _sockets.Clear();
    }
}

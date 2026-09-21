using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Netimobiledevice.Bonjour;

internal sealed class MdnsNetworkAdapters {
    private readonly List<AdapterAddress> _addresses = [];

    public MdnsNetworkAdapters() {
        foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces()) {
            IPInterfaceProperties properties = adapter.GetIPProperties();

            foreach (UnicastIPAddressInformation unicast in properties.UnicastAddresses) {
                IPAddress ip = unicast.Address;

                _addresses.Add(
                    new AdapterAddress(
                        adapter.Name,
                        adapter.Description,
                        ip,
                        unicast.PrefixLength));
            }
        }
    }

    private static bool IsInSameSubnet(
        IPAddress address,
        IPAddress local,
        int prefixLength) {
        byte[] a = address.GetAddressBytes();
        byte[] b = local.GetAddressBytes();

        if (a.Length != b.Length) {
            return false;
        }

        int fullBytes = prefixLength / 8;
        int remainingBits = prefixLength % 8;

        for (int i = 0; i < fullBytes; i++) {
            if (a[i] != b[i]) {
                return false;
            }
        }

        if (remainingBits == 0) {
            return true;
        }

        byte mask = (byte) (0xFF << (8 - remainingBits));

        return (a[fullBytes] & mask) ==
               (b[fullBytes] & mask);
    }

    public string? PickInterface(
        string ipString,
        AddressFamily family,
        int? scopeId) {
        if (family == AddressFamily.InterNetworkV6 &&
            ipString.StartsWith(
                "fe80:",
                StringComparison.OrdinalIgnoreCase) &&
            scopeId is > 0) {
            try {
                NetworkInterface? ni =
                    NetworkInterface
                        .GetAllNetworkInterfaces()
                        .FirstOrDefault(
                            x =>
                                x.GetIPProperties()
                                    .GetIPv6Properties()
                                    ?.Index ==
                                scopeId.Value);

                if (ni is not null) {
                    return ni.Name;
                }
            }
            catch {
            }
        }

        if (!IPAddress.TryParse(ipString, out IPAddress? ip)) {
            return null;
        }

        string? best = null;
        int bestPrefix = -1;

        foreach (AdapterAddress address in _addresses) {
            if (address.Address.AddressFamily != family) {
                continue;
            }

            if (!IsInSameSubnet(
                    ip,
                    address.Address,
                    address.PrefixLength)) {
                continue;
            }

            if (address.PrefixLength > bestPrefix) {
                bestPrefix = address.PrefixLength;
                best = address.Name;
            }
        }

        return best;
    }
}

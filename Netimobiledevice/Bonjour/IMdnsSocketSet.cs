using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Bonjour;

public interface IMdnsSocketSet : IDisposable {
    IAsyncEnumerable<MdnsPacket> ReceiveAsync(CancellationToken cancellationToken = default);
    Task SendQueryAsync(byte[] packet, CancellationToken cancellationToken = default);
    Task SendToAllAsync(byte[] packet, IReadOnlyList<string> ipv4Addresses, CancellationToken cancellationToken = default);
}

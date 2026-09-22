using Netimobiledevice.Bonjour;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace NetimobiledeviceTest.Bonjour;

internal sealed class FakeMdnsSocketSet : IMdnsSocketSet {
    private readonly Channel<MdnsPacket> _packets = Channel.CreateUnbounded<MdnsPacket>();

    public TaskCompletionSource<byte[]> PacketSent { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public List<byte[]> SentPackets { get; } = [];

    public bool Disposed { get; private set; }

    public FakeMdnsSocketSet(IEnumerable<MdnsPacket> packets) {
        foreach (MdnsPacket packet in packets) {
            _packets.Writer.TryWrite(packet);
        }
    }

    public void InjectIncoming(MdnsPacket packet) {
        _packets.Writer.TryWrite(packet);
    }

    public Task SendQueryAsync(
        byte[] packet,
        CancellationToken cancellationToken = default) {
        SentPackets.Add([.. packet]);
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<MdnsPacket> ReceiveAsync(
        [EnumeratorCancellation]
        CancellationToken cancellationToken = default) {
        await foreach (MdnsPacket packet in
                       _packets.Reader.ReadAllAsync(cancellationToken)) {
            yield return packet;
        }
    }

    public Task SendToAllAsync(
        byte[] packet,
        IReadOnlyList<string> ipv4Addresses,
        CancellationToken cancellationToken = default) {
        SentPackets.Add([.. packet]);
        PacketSent.TrySetResult([.. packet]);
        return Task.CompletedTask;
    }

    public void Dispose() {
        Disposed = true;
        _packets.Writer.TryComplete();
    }
}

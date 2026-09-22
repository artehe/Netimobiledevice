using Netimobiledevice.Bonjour;

namespace NetimobiledeviceTest.Bonjour;

internal sealed class FakeMdnsSocketFactory(params MdnsPacket[] packets) : IMdnsSocketFactory {
    public FakeMdnsSocketSet SocketSet { get; } = new FakeMdnsSocketSet(packets);

    public IMdnsSocketSet Open() {
        return SocketSet;
    }
}

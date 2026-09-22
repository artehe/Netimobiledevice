namespace Netimobiledevice.Bonjour;

internal sealed class MdnsSocketFactory : IMdnsSocketFactory {
    public IMdnsSocketSet Open() {
        return MdnsSocketSet.Open();
    }
}

namespace Netimobiledevice.Bonjour;

internal sealed class MdnsConstants {
    public const string Mobdev2ServiceName = "_apple-mobdev2._tcp.local.";
    public const string RemotedServiceName = "_remoted._tcp.local.";
    public const string RemotePairingManualPairingServiceName = "_remotepairing-manual-pairing._tcp.local.";
    public const string RemotePairingPairableHostServiceName = "_remotepairing-pairable-host._tcp.local.";
    public const string RemotePairingServiceName = "_remotepairing._tcp.local.";

    public const string MdnsMulticastV4 = "224.0.0.251";
    public const string MdnsMulticastV6 = "ff02::fb";
    public const int MdnsPort = 5353;

    public const ushort QTypeA = 1;
    public const ushort QTypePtr = 12;
    public const ushort QTypeTxt = 16;
    public const ushort QTypeAaaa = 28;
    public const ushort QTypeSrv = 33;

    public const ushort ClassIn = 0x0001;
    public const ushort ClassQu = 0x8000;
}

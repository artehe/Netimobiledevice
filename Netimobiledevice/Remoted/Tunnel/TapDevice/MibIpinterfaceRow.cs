using System.Runtime.InteropServices;

namespace Netimobiledevice.Remoted.Tunnel.TapDevice;

[StructLayout(LayoutKind.Sequential)]
public struct MIB_IPINTERFACE_ROW
{
    // ADDRESS_FAMILY (typically an enum in C, use ushort or uint in C# for platform compatibility)
    public ushort Family;

    // NET_LUID (64-bit unsigned integer)
    public ulong InterfaceLuid;

    // NET_IFINDEX (32-bit unsigned integer)
    public uint InterfaceIndex;

    // ULONG (32-bit unsigned integer)
    public uint MaxReassemblySize;

    // ULONG64 (64-bit unsigned integer)
    public ulong InterfaceIdentifier;

    // ULONG (32-bit unsigned integer)
    public uint MinRouterAdvertisementInterval;
    public uint MaxRouterAdvertisementInterval;

    // BOOLEAN (8-bit unsigned integer for C# equivalent)
    [MarshalAs(UnmanagedType.U1)]
    public bool AdvertisingEnabled;

    [MarshalAs(UnmanagedType.U1)]
    public bool ForwardingEnabled;

    [MarshalAs(UnmanagedType.U1)]
    public bool WeakHostSend;

    [MarshalAs(UnmanagedType.U1)]
    public bool WeakHostReceive;

    [MarshalAs(UnmanagedType.U1)]
    public bool UseAutomaticMetric;

    [MarshalAs(UnmanagedType.U1)]
    public bool UseNeighborUnreachabilityDetection;

    [MarshalAs(UnmanagedType.U1)]
    public bool ManagedAddressConfigurationSupported;

    [MarshalAs(UnmanagedType.U1)]
    public bool OtherStatefulConfigurationSupported;

    [MarshalAs(UnmanagedType.U1)]
    public bool AdvertiseDefaultRoute;

    // NL_ROUTER_DISCOVERY_BEHAVIOR (probably an enum, define as uint)
    public uint RouterDiscoveryBehavior;

    // ULONG (32-bit unsigned integer)
    public uint DadTransmits;
    public uint BaseReachableTime;
    public uint RetransmitTime;
    public uint PathMtuDiscoveryTimeout;

    // NL_LINK_LOCAL_ADDRESS_BEHAVIOR (probably an enum, define as uint)
    public uint LinkLocalAddressBehavior;

    // ULONG (32-bit unsigned integer)
    public uint LinkLocalAddressTimeout;

    // Array for ZoneIndices (defined size: ScopeLevelCount, likely a fixed size, assume 16 here)
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
    public uint[] ZoneIndices;

    public uint SitePrefixLength;
    public uint Metric;
    public uint NlMtu;

    [MarshalAs(UnmanagedType.U1)]
    public bool Connected;

    [MarshalAs(UnmanagedType.U1)]
    public bool SupportsWakeUpPatterns;

    [MarshalAs(UnmanagedType.U1)]
    public bool SupportsNeighborDiscovery;

    [MarshalAs(UnmanagedType.U1)]
    public bool SupportsRouterDiscovery;

    public uint ReachableTime;

    // NL_INTERFACE_OFFLOAD_ROD (likely a struct; define it separately)
    public NL_INTERFACE_OFFLOAD_ROD TransmitOffload;
    public NL_INTERFACE_OFFLOAD_ROD ReceiveOffload;

    [MarshalAs(UnmanagedType.U1)]
    public bool DisableDefaultRoutes;
}

[StructLayout(LayoutKind.Sequential)]
public struct NL_INTERFACE_OFFLOAD_ROD
{
    [MarshalAs(UnmanagedType.U1)]
    public bool NlChecksumSupported;

    [MarshalAs(UnmanagedType.U1)]
    public bool NlOptionsSupported;

    [MarshalAs(UnmanagedType.U1)]
    public bool TlDatagramChecksumSupported;

    [MarshalAs(UnmanagedType.U1)]
    public bool TlStreamChecksumSupported;

    [MarshalAs(UnmanagedType.U1)]
    public bool TlStreamOptionsSupported;

    [MarshalAs(UnmanagedType.U1)]
    public bool FastPathCompatible;

    [MarshalAs(UnmanagedType.U1)]
    public bool TlLargeSendOffloadSupported;

    [MarshalAs(UnmanagedType.U1)]
    public bool TlGiantSendOffloadSupported;
}

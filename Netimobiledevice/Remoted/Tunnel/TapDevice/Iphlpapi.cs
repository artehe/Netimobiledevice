using System.Runtime.InteropServices;

namespace Netimobiledevice.Remoted.Tunnel.TapDevice;

internal static partial class Iphlpapi {
    private const string DyName = "Iphlpapi";

    [LibraryImport(DyName, EntryPoint = "GetIpInterfaceEntry", SetLastError = true)]
    internal static partial nint GetIpInterfaceEntry(ref MIB_IPINTERFACE_ROW row);

    [LibraryImport(DyName, EntryPoint = "InitializeIpInterfaceEntry", SetLastError = true)]
    internal static partial void InitializeIpInterfaceEntry(out MIB_IPINTERFACE_ROW row);

    [LibraryImport(DyName, EntryPoint = "SetIpInterfaceEntry", SetLastError = true)]
    internal static partial nint SetIpInterfaceEntry(ref MIB_IPINTERFACE_ROW row);
}

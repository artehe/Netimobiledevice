using System.Runtime.InteropServices;

namespace Netimobiledevice.Remoted.Tunnel.TapDevice
{
    internal static partial class Iphlpapi
    {
        private const string DyName = "Iphlpapi";

        [DllImport(DyName, EntryPoint = "GetIpInterfaceEntry", SetLastError = true)]
        internal static extern nint GetIpInterfaceEntry(ref MIB_IPINTERFACE_ROW row);

        [DllImport(DyName, EntryPoint = "InitializeIpInterfaceEntry", SetLastError = true)]
        internal static extern void InitializeIpInterfaceEntry(out MIB_IPINTERFACE_ROW row);

        [DllImport(DyName, EntryPoint = "SetIpInterfaceEntry", SetLastError = true)]
        internal static extern nint SetIpInterfaceEntry(ref MIB_IPINTERFACE_ROW row);
    }
}

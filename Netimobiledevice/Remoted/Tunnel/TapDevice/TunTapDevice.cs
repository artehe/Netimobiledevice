using Netimobiledevice.Remoted.Tunnel.TapDevice;
using System;
using System.Diagnostics;
using System.Net.Sockets;
using NetWintun;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Remoted.Tunnel
{
    public class TunTapDevice : IDisposable
    {
        private const string DEFAULT_ADAPTER_NAME = "netwintun";

        private readonly Adapter _handle;
        private Session? _session;

        public string Name { get; }

        public void SetAddress(string value)
        {
            // Create the command
            string command = $"netsh interface ipv6 set address interface={InterfaceIndex} address={value}/64";

            // Initialize the process
            Process process = new Process();
            process.StartInfo.FileName = "cmd.exe";
            process.StartInfo.Arguments = $"/C {command}"; // Use /C to run the command and then terminate cmd
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.UseShellExecute = false; // Must be false to redirect output
            process.StartInfo.CreateNoWindow = true; // Hides the command window

            // Start the process and wait for it to exit
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            // Combine output and error messages for simplicity
            if (!string.IsNullOrEmpty(error))
            {
                throw new Exception($"Failed to set IPv6 address. Error: {error}");
            }
        }

        public uint InterfaceIndex {
            get {
                return IpInterfaceEntry.InterfaceIndex;
            }
        }

        public MIB_IPINTERFACE_ROW IpInterfaceEntry {
            get {
                MIB_IPINTERFACE_ROW row = new MIB_IPINTERFACE_ROW();
                Iphlpapi.InitializeIpInterfaceEntry(out row);

                row.InterfaceLuid = _handle.GetLuid();
                row.Family = (ushort) AddressFamily.InterNetworkV6;

                nint result = Iphlpapi.GetIpInterfaceEntry(ref row);
                if (result != 0) {
                    throw new Exception($"Failed to get IP interface entry, error code: {result}");
                }
                return row;
            }
            set {
                nint result = Iphlpapi.SetIpInterfaceEntry(ref value);
                if (result != 0) {
                    throw new Exception($"Failed to set adapter MTU, error code: {result}");
                }
            }
        }

        public ulong Luid {
            get {
                ulong luid = _handle.GetLuid();
                MIB_IPINTERFACE_ROW row = new MIB_IPINTERFACE_ROW();
                Iphlpapi.InitializeIpInterfaceEntry(out row);
                return luid;
            }
        }

        public uint Mtu {
            get {
                return IpInterfaceEntry.NlMtu;
            }
            set {
                MIB_IPINTERFACE_ROW row = IpInterfaceEntry;
                row.NlMtu = value;
                IpInterfaceEntry = row;
            }
        }

        public TunTapDevice(string name = DEFAULT_ADAPTER_NAME)
        {
            Name = name;

            // Create an adapter
            string tunnelType = Guid.NewGuid().ToString();
            Guid requestedGuid = Guid.NewGuid();
            _handle = Adapter.Create(Name, tunnelType, requestedGuid);
        }

        ~TunTapDevice()
        {
            Dispose();
        }

        public void Dispose()
        {
            Close();
            GC.SuppressFinalize(this);
        }

        public void Close()
        {
            Down();
            _handle.Dispose();
        }

        public void Down()
        {
            _session?.Dispose();
            _session = null;
        }

        public async Task<byte[]> ReadAsync(CancellationToken cancellationToken = default)
        {
            if (_session == null) {
                return [];
            }

            byte[] packetData = await _session.ReceivePacketAsync(cancellationToken);
            // Make sure we only output IPv6 packets
            return packetData[0] >> 4 != 6 
                ? [] 
                : packetData;
        }

        public void Up(uint capacity = Wintun.Constants.MaxRingCapacity)
        {
            _session = _handle.StartSession(capacity);
        }

        public void Write(ReadOnlySpan<byte> data)
        {
            if (data[0] == 0x00 && data[1] == 0x00 && data[2] == 0x86 && data[3] == 0xDD) {
                data = data[4..];
            }
            _session?.SendPacket(data);
        }
    }
}

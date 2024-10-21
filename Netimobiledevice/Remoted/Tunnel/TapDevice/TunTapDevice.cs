using Netimobiledevice.Remoted.Tunnel.TapDevice;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using WinTun;

namespace Netimobiledevice.Remoted.Tunnel
{
    public class TunTapDevice : IDisposable
    {
        private const string DEFAULT_ADAPTER_NAME = "netwintun";
        private const uint DEFAULT_RING_CAPCITY = 0x400000;

        private readonly Adapter _handle;
        private Session? _session;

        public string Name { get; }

        public string Address {
            get {
                return string.Empty;
            }

            set {
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
                if (!string.IsNullOrEmpty(error)) {
                    throw new Exception($"Failed to set IPv6 address. Error: {error}");
                }
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
            _handle.Close();
        }

        public void Down()
        {
            _session?.Close();
            _session = null;
        }

        public byte[] Read()
        {
            Packet packet = new Packet();
            bool success = _session?.ReceivePacket(out packet) ?? false;
            if (!success) {
                return [];
            }

            byte[] packetData = [.. packet.Span];
            _session?.ReleaseReceivePacket(packet);

            if (packetData[0] >> 4 != 6) {
                // Make sure we only output IPv6 packets
                return [];
            }
            return packetData;
        }

        public void Up(uint capacity = DEFAULT_RING_CAPCITY)
        {
            _session = _handle.StartSession(capacity);
        }

        public void Write(byte[] data)
        {
            if (data[0] == 0x00 && data[1] == 0x00 && data[2] == 0x86 && data[3] == 0xDD) {
                data = data.Skip(4).ToArray();
            }

            Packet packet = new Packet();
            _session?.AllocateSendPacket((uint) data.Length, out packet);
            for (int i = 0; i < packet.Span.Length; i++) {
                packet.Span[i] = data[i];
            }
            _session?.SendPacket(packet);
        }
    }
}

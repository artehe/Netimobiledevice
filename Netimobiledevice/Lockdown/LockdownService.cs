using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Netimobiledevice.Plist;
using Netimobiledevice.Remoted.Bonjour;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.NetworkInformation;
using Zeroconf;

namespace Netimobiledevice.Lockdown;

public abstract class LockdownService : IDisposable {
    protected LockdownServiceProvider Lockdown { get; }
    /// <summary>
    /// The internal logger
    /// </summary>
    protected ILogger Logger { get; }
    protected ServiceConnection Service { get; }
    protected string ServiceName { get; }

    /// <summary>
    /// Create a new LockdownService instance
    /// </summary>
    /// <param name="lockdown">Service provider</param>
    /// <param name="serviceName">The service name to attempt to connect to</param>
    /// <param name="service">An established service connection, if none we will attempt connecting to the provided serviceName</param>
    /// <param name="useEscrowBag">Use the available lockdown escrow back to start the service</param>
    public LockdownService(LockdownServiceProvider lockdown, string serviceName, ServiceConnection? service = null, bool useEscrowBag = false, ILogger? logger = null) {
        Lockdown = lockdown;
        Logger = logger ?? NullLogger.Instance;
        ServiceName = serviceName;
        Service = service ?? lockdown.StartLockdownService(ServiceName, useEscrowBag);
    }

    /// <summary>
    /// Create a new LockdownService instance
    /// </summary>
    /// <param name="lockdown">Service provider</param>
    /// <param name="lockdownServiceName">The service name to attempt to connect to if we have a Lockdown connection</param>
    /// <param name="rsdServiceName">The service name to attempt to connect to if we have an RSD connection</param>
    /// <param name="service">An established service connection, if none we will attempt connecting to the provided serviceName</param>
    /// <param name="useEscrowBag">Use the available lockdown escrow back to start the service</param>
    public LockdownService(LockdownServiceProvider lockdown, string lockdownServiceName, string rsdServiceName, ServiceConnection? service = null, bool useEscrowBag = false, ILogger? logger = null) {
        if (lockdown is LockdownClient) {
            ServiceName = lockdownServiceName;
        }
        else {
            ServiceName = rsdServiceName;
        }

        Lockdown = lockdown;
        Logger = logger ?? NullLogger.Instance;
        Service = service ?? lockdown.StartLockdownService(ServiceName, useEscrowBag);
    }

    public void Close() {
        Service.Close();
    }

    public virtual void Dispose() {
        Close();
        GC.SuppressFinalize(this);
    }

    public static async IAsyncEnumerable<(string, TcpLockdownClient)> GetMobdev2Lockdowns(
        string? udid = null,
        string? pairRecordsPath = null,
        bool onlyPaired = false,
        int timeout = BonjourService.DEFAULT_BONJOUR_TIMEOUT,
        List<NetworkInterface>? ips = null
    ) {
        Dictionary<string, DictionaryNode> records = [];
        DirectoryInfo pairRecordsDirectory = new DirectoryInfo(pairRecordsPath ?? "");
        foreach (FileInfo file in pairRecordsDirectory.GetFiles("*.plist")) {
            if (file.Name.StartsWith("remote_", StringComparison.InvariantCulture)) {
                // Skip RemotePairing records
                continue;
            }

            string recordUdid = file.Name.Replace(".plist", "");
            if (udid != null && recordUdid != udid) {
                continue;
            }

            DictionaryNode record = PropertyList.LoadFromByteArray(File.ReadAllBytes(file.FullName)).AsDictionaryNode();
            string wiFiMACAddress = record["WiFiMACAddress"].AsStringNode().Value;
            records.Add(wiFiMACAddress, record);
        }

        List<string> iteratedIps = [];
        foreach (IZeroconfHost answer in await BonjourService.BrouseMobdev2(timeout, ips).ConfigureAwait(false)) {
            if (!answer.DisplayName.Contains('@')) {
                continue;
            }
            string wifiMacAddress = answer.DisplayName.Split('@', 1)[0];
            DictionaryNode record = records[wifiMacAddress];

            if (onlyPaired && record == null) {
                continue;
            }

            foreach (string? ip in answer.IPAddresses) {
                if (iteratedIps.Contains(ip)) {
                    // Skip ips we already iterated over, possibly from previous queries
                    continue;
                }

                iteratedIps.Add(ip);

                TcpLockdownClient lockdown;
                try {
                    lockdown = MobileDevice.CreateUsingTcp(hostname: ip, autopair: false, pairRecord: record);
                }
                catch (Exception) {
                    continue;
                }

                if (onlyPaired && !lockdown.IsPaired) {
                    lockdown.Close();
                    continue;
                }
                yield return (ip, lockdown);
            }
        }
    }
}

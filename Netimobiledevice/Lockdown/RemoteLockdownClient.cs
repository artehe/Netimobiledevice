using Microsoft.Extensions.Logging;
using Netimobiledevice.Lockdown.Pairing;
using Netimobiledevice.Plist;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Lockdown
{
    public class RemoteLockdownClient : LockdownClient
    {
        /// <summary>
        /// Create a LockdownClient instance
        /// </summary>
        /// <param name="service">lockdownd connection handler</param>
        /// <param name="hostId">Used as the host identifier for the handshake</param>
        /// <param name="identifier">Used as an identifier to look for the device pair record</param>
        /// <param name="label">lockdownd user-agent</param>
        /// <param name="systemBuid">System's unique identifier</param>
        /// <param name="pairRecord">Use this pair record instead of the default behavior (search in host/create our own)</param>
        /// <param name="pairingRecordsCacheDirectory">Use the following location to search and save pair records</param>
        /// <param name="port">lockdownd service port</param>
        /// <param name="logger"></param>
        public RemoteLockdownClient(ServiceConnection service, string hostId, string identifier = "", string label = DEFAULT_CLIENT_NAME, string systemBuid = SYSTEM_BUID, DictionaryNode? pairRecord = null, DirectoryInfo? pairingRecordsCacheDirectory = null, ushort port = SERVICE_PORT, ILogger? logger = null) : base(service, hostId, identifier, label, systemBuid, pairRecord, pairingRecordsCacheDirectory, port, logger)
        {
        }

        protected override void HandleAutoPair(bool autoPair, float timeout)
        {
            // RemoteXPC lockdown version does not support pairing operations
            return;
        }

        /// <summary>
        /// Create a LockdownClient instance
        /// </summary>
        /// <param name="service">lockdownd connection handler</param>
        /// <param name="identifier">Used as an identifier to look for the device pair record</param>
        /// <param name="systemBuid">System's unique identifier</param>
        /// <param name="label">lockdownd user-agent</param>
        /// <param name="autopair">Attempt to pair with device (blocking) if not already paired</param>
        /// <param name="pairTimeout">Timeout for autopair</param>
        /// <param name="localHostname">Used as a seed to generate the HostID</param>
        /// <param name="pairRecord">Use this pair record instead of the default behavior (search in host/create our own)</param>
        /// <param name="pairingRecordsCacheFolder">Use the following location to search and save pair records</param>
        /// <param name="port">lockdownd service port</param>
        /// <returns>A new LockdownClient instance</returns>
        public static RemoteLockdownClient Create(ServiceConnection service, string identifier = "", string systemBuid = SYSTEM_BUID, string label = DEFAULT_CLIENT_NAME,
            bool autopair = true, float? pairTimeout = null, string localHostname = "", DictionaryNode? pairRecord = null, string pairingRecordsCacheFolder = "",
            ushort port = SERVICE_PORT, ILogger? logger = null)
        {
            string hostId = PairRecords.GenerateHostId(localHostname);
            DirectoryInfo? pairingRecordsCacheDirectory = PairRecords.GetPairingRecordsCacheFolder(pairingRecordsCacheFolder);

            RemoteLockdownClient lockdownClient = new(service, hostId: hostId, identifier: identifier, label: label, systemBuid: systemBuid, pairRecord: pairRecord,
                pairingRecordsCacheDirectory: pairingRecordsCacheDirectory, port: port, logger: logger);

            lockdownClient.HandleAutoPair(autopair, pairTimeout ?? -1);
            return lockdownClient;
        }

        public override Task<ServiceConnection> CreateServiceConnection(ushort port)
        {
            throw new NotImplementedException("RemoteXPC service connections should only be created using RemoteServiceDiscoveryService");

        }

        public override Task<bool> PairAsync()
        {
            throw new NotImplementedException("RemoteXPC lockdown version does not support pairing operations");
        }

        public override Task<bool> PairAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException("RemoteXPC lockdown version does not support pairing operations");
        }

        public override Task<bool> PairAsync(IProgress<PairingState> progress)
        {
            throw new NotImplementedException("RemoteXPC lockdown version does not support pairing operations");
        }

        public override Task<bool> PairAsync(IProgress<PairingState> progress, CancellationToken cancellationToken)
        {
            throw new NotImplementedException("RemoteXPC lockdown version does not support pairing operations");
        }

        public override bool PairDevice()
        {
            throw new NotImplementedException("RemoteXPC lockdown version does not support pairing operations");
        }

        public override void Unpair()
        {
            throw new NotImplementedException("RemoteXPC lockdown version does not support pairing operations");
        }
    }
}

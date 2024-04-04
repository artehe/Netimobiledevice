using System;
using System.IO;
using System.Net;

namespace Netimobiledevice.Lockdown.Pairing
{
    internal static class PairRecords
    {
        public static string GenerateHostId(string hostname = "")
        {
            if (string.IsNullOrEmpty(hostname)) {
                hostname = Dns.GetHostName();
            }

            bool success = Guid.TryParse(hostname, out Guid result);
            if (success) {
                return result.ToString().ToUpperInvariant();
            }
            return Guid.NewGuid().ToString().ToUpperInvariant();
        }

        public static DirectoryInfo CreatePairingRecordsCacheFolder(string pairingRecordsCacheFolder = "")
        {
            if (string.IsNullOrEmpty(pairingRecordsCacheFolder)) {
                pairingRecordsCacheFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Netimobiledevice");
            }
            Directory.CreateDirectory(pairingRecordsCacheFolder);
            return new DirectoryInfo(pairingRecordsCacheFolder);
        }
    }
}

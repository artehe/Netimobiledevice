using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Netimobiledevice.Plist;
using Netimobiledevice.Usbmuxd;
using System;
using System.IO;
using System.Net;

namespace Netimobiledevice.Lockdown.Pairing;

internal static class PairRecords
{
    private static DictionaryNode? GetItunesPairingRecord(string identifier, ILogger logger)
    {
        string filePath = $"{identifier}.plist";
        if (OperatingSystem.IsMacOS()) {
            filePath = Path.Combine("/var/db/lockdown/", filePath);
        }
        else if (OperatingSystem.IsLinux()) {
            filePath = Path.Combine("/var/lib/lockdown/", filePath);
        }
        else if (OperatingSystem.IsWindows()) {
            filePath = Path.Combine(@"C:\ProgramData\Apple\Lockdown", filePath);
        }
        else {
            throw new NotSupportedException("Getting paring record for this OS is not supported.");
        }

        try {
            if (File.Exists(filePath)) {
                using (FileStream fs = File.OpenRead(filePath)) {
                    return PropertyList.Load(fs).AsDictionaryNode();
                }
            }
        }
        catch (UnauthorizedAccessException ex) {
            logger.LogWarning(ex, "Warning unauthorised access excpetion when trying to access itunes plist");
        }
        return null;
    }

    public static DirectoryInfo? GetPairingRecordsCacheFolder(string pairingRecordsCacheFolder = "")
    {
        if (string.IsNullOrEmpty(pairingRecordsCacheFolder)) {
            return null;
        }
        return new DirectoryInfo(pairingRecordsCacheFolder);
    }

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

    public static DictionaryNode? GetLocalPairingRecord(string identifier, DirectoryInfo? pairingRecordsCacheDirectory, ILogger logger)
    {
        logger.LogDebug("Looking for Netimobiledevice pairing record");
        string filePath = $"{identifier}.plist";
        if (pairingRecordsCacheDirectory != null) {
            filePath = Path.Combine(pairingRecordsCacheDirectory.FullName, filePath);
        }

        if (File.Exists(filePath)) {
            using (FileStream fs = File.OpenRead(filePath)) {
                return PropertyList.Load(fs).AsDictionaryNode();
            }
        }
        else {
            logger.LogDebug("No Netimobiledevice pairing record found for device {identifier}", identifier);
            return null;
        }
    }

    /// <summary>
    /// Looks for an existing pair record for the connected device in the following order:
    ///  - Usbmuxd
    ///  - iTunes
    ///  - Local Storage
    /// </summary>
    public static DictionaryNode GetPreferredPairRecord(string identifier, DirectoryInfo? pairingRecordsCacheDirectory, string usbmuxAddress = "", ILogger? logger = null)
    {
        logger ??= NullLogger.Instance;
        DictionaryNode? pairRecord = null;

        // First look for the usbmuxd pair record
        try {
            using (UsbmuxConnection muxConnection = UsbmuxConnection.Create(usbmuxAddress, logger)) {
                if (muxConnection is PlistMuxConnection plistMuxConnection) {
                    pairRecord = plistMuxConnection.GetPairRecord(identifier);
                    if (pairRecord != null && pairRecord.Count > 0) {
                        logger.LogDebug("Using usbmuxd pair record for identifier: {identifier}", identifier);
                        return pairRecord;
                    }
                }
            }
        }
        catch (Exception ex) {
            // These are expected and if we get them can be ignored
            if (ex is not NotPairedException && ex is not UsbmuxException) {
                throw;
            }
        }

        // Second look for an iTunes pair record
        pairRecord = GetItunesPairingRecord(identifier, logger);
        if (pairRecord != null && pairRecord.Count > 0) {
            logger.LogDebug("Using iTunes pair record");
            return pairRecord;
        }

        // Lastly look for a local pair record
        pairRecord = GetLocalPairingRecord(identifier, pairingRecordsCacheDirectory, logger);
        if (pairRecord != null && pairRecord.Count > 0) {
            logger.LogDebug("Using local pair record: {identifier}.plist", identifier);
            return pairRecord;
        }

        // We didn't find any records so throw a not paired exception
        throw new NotPairedException();
    }

    public static string GetRemotePairingRecordFilename(string identifier)
    {
        return $"remote_{identifier}";
    }

    /// <summary>
    /// Iterate over the identifiers of the remote paired devices.
    /// </summary>
    /// <returns> A enumerator yielding the identifiers of the remote paired devices.</returns>
    public static IEnumerable<string> IterateRemotePairedIdentifiers()
    {
        foreach (var file in IterateRemotePairRecords()) {
            // yield file.parts[-1].split('remote_', 1)[1].split('.', 1)[0]
        }
    }
}

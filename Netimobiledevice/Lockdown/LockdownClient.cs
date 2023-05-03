using Netimobiledevice.Exceptions;
using Netimobiledevice.Plist;
using Netimobiledevice.Usbmuxd;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace Netimobiledevice.Lockdown
{
    public class LockdownClient
    {
        private const string DEFAULT_CLIENT_NAME = "Netimobiledevice";
        private const ushort SERVICE_PORT = 62078;

        private string deviceClass;
        private byte[] devicePublicKey;
        /// <summary>
        /// User agent to use when identifying for lockdownd
        /// </summary>
        private readonly string label = DEFAULT_CLIENT_NAME;
        private bool paired = false;
        private string hostId;
        private string identifier;
        private ConnectionMedium medium;
        /// <summary>
        /// The pairing record for the connected device
        /// </summary>
        private string pairingRecordsCacheDir;
        private DictionaryNode? pairRecord;
        private Version productVersion;
        private ServiceConnection service;
        private string sessionId;
        private string systemBUID;
        private string udid;
        private DictionaryNode allValues;
        private UsbmuxdConnectionType usbmuxdConnectionType;

        private StringNode WifiMacAddress => allValues["WiFiAddress"].AsStringNode();

        /// <summary>
        /// Create the LockdownClient
        /// </summary>
        /// <param name="serial">Serial number for device to connect to (over usbmux)</param>
        /// <param name="connectionType">Specify what connection type to use</param>
        /// <param name="autoPair">Should pairing with the device be automatically attempted</param>
        /// <param name="pairTimeout">if using autoPair, this timeout is for the user's Trust dialog. If not set will wait forever</param>
        public LockdownClient(
            string? serial = null,
            UsbmuxdConnectionType connectionType = UsbmuxdConnectionType.Usb,
            bool autoPair = true,
            int pairTimeout = -1,
            string pairingRecordsCacheDir = null)
        {
            identifier = serial ?? string.Empty;
            medium = ConnectionMedium.USBMUX;
            usbmuxdConnectionType = connectionType;
            service = ServiceConnection.Create(medium, identifier, SERVICE_PORT, usbmuxdConnectionType);

            if (string.IsNullOrEmpty(pairingRecordsCacheDir)) {
                this.pairingRecordsCacheDir = string.Empty;
            }
            else {
                this.pairingRecordsCacheDir = pairingRecordsCacheDir;
                Directory.CreateDirectory(pairingRecordsCacheDir);
            }

            if (QueryType() != "com.apple.mobile.lockdown") {
                throw new IncorrectModeException();
            }

            allValues = GetValue().AsDictionaryNode();
            udid = allValues["UniqueDeviceID"].AsStringNode().Value;
            productVersion = new Version(allValues["ProductVersion"].AsStringNode().Value);

            try {
                deviceClass = DeviceClass.GetDeviceClass(allValues["DeviceClass"]);
            }
            catch (Exception) {
                deviceClass = DeviceClass.UNKNOWN;
            }

            if (string.IsNullOrEmpty(identifier) && medium == ConnectionMedium.USBMUX) {
                // Attempt get identifier from mux device serial
                identifier = service.GetUsbmuxdDevice()?.Serial ?? string.Empty;
            }
            if (string.IsNullOrEmpty(identifier) && !string.IsNullOrEmpty(udid)) {
                // Attempt get identifier from queried udid
                identifier = udid;
            }

            if (!ValidatePairing()) {
                // Device is not paired
                if (!autoPair) {
                    // Pairing by default was not requested
                    return;
                }

                Pair(pairTimeout);

                // Get sessionId
                if (!ValidatePairing()) {
                    throw new FatalPairingException();
                }
            }

            // Reload data after pairing
            allValues = GetValue().AsDictionaryNode();
            udid = allValues["UniqueDeviceID"].AsStringNode().Value;
        }

        private ServiceConnection CreateServiceConnection(ushort port)
        {
            return ServiceConnection.Create(medium, identifier, port, usbmuxdConnectionType);
        }

        private DictionaryNode? GetItunesPairingRecord()
        {
            string filePath = $"{identifier}.plist";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                filePath = Path.Combine("/var/db/lockdown/", filePath);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
                filePath = Path.Combine("/var/lib/lockdown/", filePath);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                filePath = Path.Combine("C:\\ProgramData\\Apple\\Lockdown", filePath);
            }

            if (File.Exists(filePath)) {
                using (FileStream fs = File.OpenRead(filePath)) {
                    return PropertyList.Load(fs).AsDictionaryNode();
                }
            }
            return null;
        }

        private DictionaryNode? GetLocalPairingRecord()
        {
            Debug.WriteLine("Looking for Netimobiledevice pairing record");

            string filePath = $"{identifier}.plist";
            if (string.IsNullOrEmpty(pairingRecordsCacheDir)) {
                filePath = Path.Combine(pairingRecordsCacheDir, filePath);
            }

            if (!File.Exists(filePath)) {
                Debug.WriteLine($"No Netimobiledevice pairing record found for device {identifier}");
                return null;
            }

            using (FileStream fs = File.OpenRead(filePath)) {
                return PropertyList.Load(fs).AsDictionaryNode();
            }
        }

        private PropertyNode GetServiceConnectionAttributes(string name)
        {
            if (!paired) {
                throw new NotPairedException();
            }

            DictionaryNode options = new DictionaryNode {
                { "Service", new StringNode(name) }
            };

            DictionaryNode response = Request("StartService", options).AsDictionaryNode();
            if (response.ContainsKey("Error")) {
                string error = response["Error"].AsStringNode().Value;
                if (error == "PasswordProtected") {
                    throw new PasswordRequiredException("Your device is protected, please enter your passcode and try again");
                }
                else {
                    throw new ServiceStartException(error);
                }
            }
            return response;
        }

        private PropertyNode GetValue(string? domain = null, string? key = null)
        {
            DictionaryNode options = new DictionaryNode();
            if (!string.IsNullOrEmpty(domain)) {
                options.Add("Domain", new StringNode(domain));
            }
            if (!string.IsNullOrEmpty(key)) {
                options.Add("Key", new StringNode(key));
            }

            DictionaryNode result = Request("GetValue", options).AsDictionaryNode();

            if (result.ContainsKey("Data")) {
                return result["Data"];
            }
            return result["Value"];
        }

        /// <summary>
        /// Looks for an existing pair record for the connected device in the following order:
        ///  - iTunes
        ///  - Usbmuxd
        ///  - Local Storage
        /// </summary>
        private void InitPreferredPairRecord()
        {
            if (pairRecord != null) {
                // If we already have on then use that
                return;
            }

            // First look for an iTunes pair record
            pairRecord = GetItunesPairingRecord();
            if (pairRecord != null) {
                Debug.WriteLine("Using iTunes pair record");
                return;
            }

            // Second look for the usbmuxd pair record
            UsbmuxConnection mux = UsbmuxConnection.Create();
            if (medium == ConnectionMedium.USBMUX && mux is PlistMuxConnection plistMuxConnection) {
                pairRecord = plistMuxConnection.GetPairRecord(identifier);
            }
            mux.Close();
            if (pairRecord != null) {
                Debug.WriteLine($"Using usbmuxd pair record for identifier: {identifier}");
                return;
            }

            // Lastly look for a local pair record
            pairRecord = GetLocalPairingRecord();
            if (pairRecord != null) {
                Debug.WriteLine($"Using local pair record: {identifier}.plist");
            }
        }

        private PropertyNode Request(string request, DictionaryNode? options = null, bool verifyRequest = true)
        {
            DictionaryNode message = new DictionaryNode {
                { "Label", new StringNode(label) },
                { "Request", new StringNode(request) }
            };
            if (options != null) {
                foreach (KeyValuePair<string, PropertyNode> option in options) {
                    message.Add(option);
                }
            }

            DictionaryNode response = service.SendReceivePlist(message)?.AsDictionaryNode() ?? new DictionaryNode();

            if (verifyRequest && response["Request"].AsStringNode().Value != request) {
                throw new LockdownException($"Incorrect response returned, as got {response["Request"].AsStringNode().Value} instead of {request}");
            }

            if (response.ContainsKey("Error")) {
                string error = response["Error"].AsStringNode().Value;
                throw error switch {
                    "InvalidHostID" => new LockdownException(LockdownError.InvalidHostID),
                    "InvalidService" => new LockdownException(LockdownError.InvalidService),
                    "MissingValue" => new LockdownException(LockdownError.MissingValue),
                    "PairingDialogResponsePending" => new LockdownException(LockdownError.PairingDialogResponsePending),
                    "PasswordProtected" => new LockdownException(LockdownError.PasswordRequired),
                    "SetProhibited" => new LockdownException(LockdownError.SetProhibited),
                    "UserDeniedPairing" => new LockdownException(LockdownError.UserDeniedPairing),
                    _ => new LockdownException(error),
                };
            }

            // On iOS < 5: "Error" doesn't exist, so we have to check for "Result" instead
            if (response.ContainsKey("Result")) {
                string error = response["Result"].AsStringNode().Value;
                if (error == "Failure") {
                    throw new LockdownException();
                }
            }

            return response;
        }

        private DictionaryNode RequestPair(DictionaryNode pairOptions, int timeout = -1)
        {
            try {
                return Request("Pair", pairOptions).AsDictionaryNode();
            }
            catch (LockdownException ex) {
                if (ex.LockdownError != LockdownError.PairingDialogResponsePending) {
                    throw;
                }
                if (ex.LockdownError == LockdownError.PairingDialogResponsePending && timeout == 0) {
                    throw;
                }
            }

            Debug.WriteLine("Waiting for user pairing dialog...");
            DateTime startTime = DateTime.Now;

            while (timeout < 0 || DateTime.Now <= startTime.AddSeconds(timeout)) {
                try {
                    return Request("Pair", pairOptions).AsDictionaryNode();
                }
                catch (LockdownException ex) {
                    if (ex.LockdownError != LockdownError.PairingDialogResponsePending) {
                        throw;
                    }
                }
                Thread.Sleep(1000);
            }

            throw new LockdownException(LockdownError.PairingDialogResponsePending);
        }

        private void Pair(int timeout = -1)
        {
            devicePublicKey = GetValue(null, "DevicePublicKey").AsDataNode().Value;
            if (devicePublicKey == null || devicePublicKey.Length == 0) {
                Debug.WriteLine("Unable to retrieve DevicePublicKey");
                service.Close();
                throw new FatalPairingException();
            }

            Debug.WriteLine("Creating host key & certificate");
            (byte[] rootCertPem, byte[] privateKeyPem, byte[] deviceCertPem) = CertificateGenerator.GeneratePairingCertificates(devicePublicKey);

            DictionaryNode newPairRecord = new DictionaryNode {
                { "DevicePublicKey", new DataNode(devicePublicKey) },
                { "DeviceCertificate", new DataNode(deviceCertPem) },
                { "HostCertificate", new DataNode(rootCertPem) },
                { "HostID", new StringNode(hostId) },
                { "RootCertificate", new DataNode(rootCertPem) },
                { "RootPrivateKey", new DataNode(privateKeyPem) },
                { "WiFiMACAddress", WifiMacAddress },
                { "SystemBUID", new StringNode(systemBUID) }
            };

            DictionaryNode pairOptions = new DictionaryNode {
                { "PairRecord", newPairRecord },
                { "ProtocolVersion", new StringNode("2") },
                { "PairingOptions", new DictionaryNode() {
                        { "ExtendedPairingErrors", new BooleanNode(true) }
                    }
                }
            };

            DictionaryNode pair = RequestPair(pairOptions, timeout);

            newPairRecord.Add("HostPrivateKey", new DataNode(privateKeyPem));
            if (pair.ContainsKey("EscrowBag")) {
                newPairRecord.Add("EscrowBag", pair["EscrowBag"]);
            }

            pairRecord = newPairRecord;
            WriteStorageFile($"{identifier}.plist", PropertyList.SaveAsByteArray(pairRecord, PlistFormat.Xml));

            if (medium == ConnectionMedium.USBMUX) {
                byte[] recordData = PropertyList.SaveAsByteArray(pairRecord, PlistFormat.Xml);

                UsbmuxConnection mux = UsbmuxConnection.Create();
                if (mux is PlistMuxConnection plistMuxConnection) {
                    plistMuxConnection.SavePairRecord(identifier, (int) service.GetUsbmuxdDevice()?.DeviceId, recordData);
                }
                mux.Close();
            }

            paired = true;
        }

        private string QueryType()
        {
            return Request("QueryType").AsDictionaryNode()["Type"].AsStringNode().Value;
        }

        private bool ValidatePairing()
        {
            try {
                InitPreferredPairRecord();
            }
            catch (NotPairedException) {
                return false;
            }

            if (pairRecord == null) {
                return false;
            }

            if (productVersion < new Version("7.0") && deviceClass != DeviceClass.WATCH) {
                try {
                    DictionaryNode options = new DictionaryNode {
                        { "PairRecord", pairRecord }
                    };
                    Request("ValidatePair", options);
                }
                catch (Exception) {
                    return false;
                }
            }

            hostId = pairRecord["HostID"].AsStringNode().Value;
            systemBUID = pairRecord["SystemBUID"].AsStringNode().Value;

            DictionaryNode startSession = new DictionaryNode();
            try {
                DictionaryNode options = new DictionaryNode {
                    { "HostID", new StringNode(hostId) },
                    { "SystemBUID", new StringNode(systemBUID) }
                };
                startSession = Request("StartSession", options).AsDictionaryNode();
            }
            catch (LockdownException ex) {
                if (ex.LockdownError == LockdownError.InvalidHostID) {
                    // No HostID means there is no such pairing record
                    return false;
                }
                else {
                    throw;
                }
            }

            sessionId = startSession["SessionID"].AsStringNode().Value;

            if (startSession.ContainsKey("EnableSessionSSL") && startSession["EnableSessionSSL"].AsBooleanNode().Value) {
                service.StartSSL(pairRecord["HostCertificate"].AsDataNode().Value, pairRecord["HostPrivateKey"].AsDataNode().Value);
            }

            paired = true;
            return true;
        }

        private void WriteStorageFile(string filename, byte[] data)
        {
            string file = Path.Combine(pairingRecordsCacheDir, filename);
            File.WriteAllBytes(file, data);
        }

        public ServiceConnection StartService(string serviceName)
        {
            DictionaryNode attr = GetServiceConnectionAttributes(serviceName).AsDictionaryNode();
            ServiceConnection serviceConnection = CreateServiceConnection((ushort) attr["Port"].AsIntegerNode().Value);

            if (attr.ContainsKey("EnableServiceSSL") && attr["EnableServiceSSL"].AsBooleanNode().Value) {
                serviceConnection.StartSSL(pairRecord["HostCertificate"].AsDataNode().Value, pairRecord["HostPrivateKey"].AsDataNode().Value);
            }
            return serviceConnection;
        }
    }
}

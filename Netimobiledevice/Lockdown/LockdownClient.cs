using Netimobiledevice.Exceptions;
using Netimobiledevice.Lockdown.Services;
using Netimobiledevice.Plist;
using Netimobiledevice.Usbmuxd;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Lockdown
{
    public class LockdownClient : IDisposable
    {
        private const string DEFAULT_CLIENT_NAME = "Netimobiledevice";
        private const ushort SERVICE_PORT = 62078;

        private byte[] devicePublicKey = Array.Empty<byte>();
        /// <summary>
        /// User agent to use when identifying for lockdownd
        /// </summary>
        private readonly string label = DEFAULT_CLIENT_NAME;
        private string hostId = string.Empty;
        private readonly ConnectionMedium medium;
        private readonly string pairingRecordsCacheDir;
        /// <summary>
        /// The pairing record for the connected device
        /// </summary>
        private DictionaryNode? pairRecord;
        private ServiceConnection? service;
        private string systemBUID = string.Empty;
        private DictionaryNode allValues = new DictionaryNode();
        private readonly UsbmuxdConnectionType usbmuxdConnectionType;

        public string DeviceClass { get; private set; } = LockdownDeviceClass.UNKNOWN;

        public string DeviceName => GetValue("DeviceName")?.AsStringNode().Value ?? string.Empty;

        public bool EnableWifiConnections {
            get => GetValue("com.apple.mobile.wireless_lockdown", "EnableWifiConnections")?.AsBooleanNode().Value ?? false;
            set => SetValue("com.apple.mobile.wireless_lockdown", "EnableWifiConnections", new BooleanNode(value));
        }

        public Version IOSVersion { get; private set; } = new Version();

        /// <summary>
        /// Is the connected iOS trusted/paired with this device.
        /// </summary>
        public bool IsPaired { get; private set; } = false;

        public string SerialNumber { get; private set; } = string.Empty;

        public string UDID { get; private set; } = string.Empty;

        public string WifiMacAddress => GetValue("WiFiAddress")?.AsStringNode().Value ?? string.Empty;

        private LockdownClient(string udid, ConnectionMedium connectionMedium)
        {
            UDID = udid;
            medium = connectionMedium;
            usbmuxdConnectionType = UsbmuxdConnectionType.Usb;
            pairingRecordsCacheDir = string.Empty;
        }

        private ServiceConnection CreateServiceConnection(ushort port)
        {
            return ServiceConnection.Create(medium, UDID, port, usbmuxdConnectionType);
        }

        private DictionaryNode? GetItunesPairingRecord()
        {
            string filePath = $"{UDID}.plist";
            if (OperatingSystem.IsMacOS()) {
                filePath = Path.Combine("/var/db/lockdown/", filePath);
            }
            else if (OperatingSystem.IsLinux()) {
                filePath = Path.Combine("/var/lib/lockdown/", filePath);
            }
            else if (OperatingSystem.IsWindows()) {
                filePath = Path.Combine("C:\\ProgramData\\Apple\\Lockdown", filePath);
            }

            try {
                if (File.Exists(filePath)) {
                    using (FileStream fs = File.OpenRead(filePath)) {
                        return PropertyList.Load(fs).AsDictionaryNode();
                    }
                }
            }
            catch (UnauthorizedAccessException ex) {
                Debug.WriteLine($"Warning unauthorised access excpetion when trying to access itunes plist: {ex}");
            }
            return null;
        }

        private DictionaryNode? GetLocalPairingRecord()
        {
            Debug.WriteLine("Looking for Netimobiledevice pairing record");

            string filePath = $"{UDID}.plist";
            if (string.IsNullOrEmpty(pairingRecordsCacheDir)) {
                filePath = Path.Combine(pairingRecordsCacheDir, filePath);
            }

            if (!File.Exists(filePath)) {
                Debug.WriteLine($"No Netimobiledevice pairing record found for device {UDID}");
                return null;
            }

            using (FileStream fs = File.OpenRead(filePath)) {
                return PropertyList.Load(fs).AsDictionaryNode();
            }
        }

        private PropertyNode GetServiceConnectionAttributes(string name)
        {
            if (!IsPaired) {
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
                pairRecord = plistMuxConnection.GetPairRecord(UDID);
            }
            mux.Close();
            if (pairRecord != null) {
                Debug.WriteLine($"Using usbmuxd pair record for identifier: {UDID}");
                return;
            }

            // Lastly look for a local pair record
            pairRecord = GetLocalPairingRecord();
            if (pairRecord != null) {
                Debug.WriteLine($"Using local pair record: {UDID}.plist");
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

            DictionaryNode response = service?.SendReceivePlist(message)?.AsDictionaryNode() ?? new DictionaryNode();

            if (verifyRequest && response["Request"].AsStringNode().Value != request) {
                throw new LockdownException($"Incorrect response returned, as got {response["Request"].AsStringNode().Value} instead of {request}");
            }

            if (response.ContainsKey("Error")) {
                string error = response["Error"].AsStringNode().Value;
                throw error switch {
                    "InvalidHostID" => new LockdownException(LockdownError.InvalidHostId),
                    "InvalidService" => new LockdownException(LockdownError.InvalidService),
                    "MissingValue" => new LockdownException(LockdownError.MissingValue),
                    "PairingDialogResponsePending" => new LockdownException(LockdownError.PairingDialogResponsePending),
                    "PasswordProtected" => new LockdownException(LockdownError.PasswordProtected),
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
            devicePublicKey = GetValue(null, "DevicePublicKey")?.AsDataNode().Value ?? Array.Empty<byte>();
            if (devicePublicKey == null || devicePublicKey.Length == 0) {
                Debug.WriteLine("Unable to retrieve DevicePublicKey");
                service?.Close();
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
                { "WiFiMACAddress", new StringNode(WifiMacAddress) },
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
            WriteStorageFile($"{UDID}.plist", PropertyList.SaveAsByteArray(pairRecord, PlistFormat.Xml));

            if (medium == ConnectionMedium.USBMUX) {
                byte[] recordData = PropertyList.SaveAsByteArray(pairRecord, PlistFormat.Xml);

                UsbmuxConnection mux = UsbmuxConnection.Create();
                if (mux is PlistMuxConnection plistMuxConnection) {
                    int deviceId = (int) (service?.GetUsbmuxdDevice()?.DeviceId ?? 0);
                    plistMuxConnection.SavePairRecord(UDID, deviceId, recordData);
                }
                mux.Close();
            }

            IsPaired = true;
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

            if (IOSVersion < new Version("7.0") && DeviceClass != LockdownDeviceClass.WATCH) {
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

            DictionaryNode startSession;
            try {
                DictionaryNode options = new DictionaryNode {
                    { "HostID", new StringNode(hostId) },
                    { "SystemBUID", new StringNode(systemBUID) }
                };
                startSession = Request("StartSession", options).AsDictionaryNode();
            }
            catch (LockdownException ex) {
                if (ex.LockdownError == LockdownError.InvalidHostId) {
                    // No HostID means there is no such pairing record
                    return false;
                }
                else {
                    throw;
                }
            }

            if (startSession.ContainsKey("EnableSessionSSL") && startSession["EnableSessionSSL"].AsBooleanNode().Value) {
                service?.StartSSL(pairRecord["HostCertificate"].AsDataNode().Value, pairRecord["HostPrivateKey"].AsDataNode().Value);
            }

            IsPaired = true;
            return IsPaired;
        }

        private void WriteStorageFile(string filename, byte[] data)
        {
            string file = Path.Combine(pairingRecordsCacheDir, filename);
            File.WriteAllBytes(file, data);
        }

        public void Dispose()
        {
            service?.Dispose();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Gets the value for the specified domain and key.
        /// </summary>
        /// <param name="domain">The domain to obtain the value from.</param>
        /// <param name="key">The key of the property to obtain.</param>
        /// <returns>The value obtained.</returns>
        public PropertyNode? GetValue(string? domain, string? key)
        {
            DictionaryNode options = new DictionaryNode();
            if (!string.IsNullOrEmpty(domain)) {
                options.Add("Domain", new StringNode(domain));
            }
            if (!string.IsNullOrEmpty(key)) {
                options.Add("Key", new StringNode(key));
            }

            try {
                DictionaryNode result = Request("GetValue", options).AsDictionaryNode();
                if (result.ContainsKey("Data")) {
                    return result["Data"];
                }
                return result["Value"];
            }
            catch (LockdownException ex) {
                if (ex.LockdownError == LockdownError.MissingValue) {
                    return null;
                }
                throw;
            }
        }

        /// <summary>
        /// Gets the value for the specified key in the root domain.
        /// </summary>
        /// <param name="key">The key of the property to obtain.</param>
        /// <returns>The string value obtained.</returns>
        public PropertyNode? GetValue(string? key)
        {
            return GetValue(null, key);
        }

        /// <summary>
        /// Get every value for the specified in the root domain.
        /// </summary>
        /// <returns>The values obtained.</returns>
        public PropertyNode? GetValue()
        {
            return GetValue(null, null);
        }

        /// <summary>
        /// Attempts to pair with the currently connected iOS device, or returns true if the device is already paired.
        /// </summary>
        /// <param name="timeout">How long to wait when pairing the iOS device</param>
        /// <returns>If the device is currently paired or if the pairing was successful or not</returns>
        /// <exception cref="FatalPairingException">Exception thrown when pairing should have succeeded but failed for some reason.</exception>
        public bool PairDevice(int timeout = -1)
        {
            bool currentlyPaird = ValidatePairing();
            if (currentlyPaird) {
                return true;
            }

            // The device is not paired so we attempt to pair it.
            Pair(timeout);

            // Get sessionId
            if (ValidatePairing()) {
                throw new FatalPairingException();
            }

            // Now we are paied, reload data
            allValues = GetValue()?.AsDictionaryNode() ?? new DictionaryNode();
            UDID = allValues["UniqueDeviceID"].AsStringNode().Value;
            return IsPaired;
        }

        public PropertyNode SetValue(string? domain, string? key, PropertyNode value)
        {
            DictionaryNode options = new DictionaryNode();

            if (!string.IsNullOrWhiteSpace(domain)) {
                options.Add("Domain", new StringNode(domain));
            }
            if (!string.IsNullOrWhiteSpace(key)) {
                options.Add("Key", new StringNode(key));
            }

            options.Add("Value", value);

            return Request("SetValue", options);
        }

        public ServiceConnection StartService(string serviceName)
        {
            DictionaryNode attr = GetServiceConnectionAttributes(serviceName).AsDictionaryNode();
            ServiceConnection serviceConnection = CreateServiceConnection((ushort) attr["Port"].AsIntegerNode().Value);

            if (attr.ContainsKey("EnableServiceSSL") && attr["EnableServiceSSL"].AsBooleanNode().Value) {
                if (pairRecord == null) {
                    throw new Exception("Pair Record is null when it shouldn't be");
                }
                serviceConnection.StartSSL(pairRecord["HostCertificate"].AsDataNode().Value, pairRecord["HostPrivateKey"].AsDataNode().Value);
            }
            return serviceConnection;
        }

        /// <summary>
        /// Try to unpair the device.
        /// </summary>
        public void Unpair()
        {
            if (pairRecord != null) {
                DictionaryNode options = new DictionaryNode() {
                    { "PairRecord", pairRecord },
                    { "ProtocolVersion", new StringNode("2")}
                };
                Request("Unpair", options, true);
                IsPaired = false;
                pairRecord = null;
            }
        }

        /// <summary>
        /// Create the LockdownClient
        /// </summary>
        /// <param name="udid">UDID of the device to connect to (over usbmux)</param>
        /// <param name="autoPair">Should pairing with the device be automatically attempted</param>
        /// <param name="connectionMedium">What medium should be used to connect to the lockdown client</param>
        public static LockdownClient CreateLockdownClient(string udid, bool autoPair = false, ConnectionMedium connectionMedium = ConnectionMedium.USBMUX)
        {
            LockdownClient client = new LockdownClient(udid, connectionMedium);
            client.service = ServiceConnection.Create(client.medium, client.UDID, SERVICE_PORT, client.usbmuxdConnectionType);

            if (client.QueryType() != "com.apple.mobile.lockdown") {
                throw new IncorrectModeException();
            }

            client.allValues = client.GetValue()?.AsDictionaryNode() ?? new DictionaryNode();
            client.UDID = client.allValues["UniqueDeviceID"].AsStringNode().Value;
            client.IOSVersion = new Version(client.allValues["ProductVersion"].AsStringNode().Value);

            try {
                client.DeviceClass = LockdownDeviceClass.GetDeviceClass(client.allValues["DeviceClass"]);
            }
            catch (Exception) {
                client.DeviceClass = LockdownDeviceClass.UNKNOWN;
            }

            if (string.IsNullOrEmpty(client.UDID) && client.medium == ConnectionMedium.USBMUX) {
                // Attempt get identifier from mux device serial
                client.UDID = client.service.GetUsbmuxdDevice()?.Serial ?? string.Empty;
            }
            if (string.IsNullOrEmpty(client.UDID) && !string.IsNullOrEmpty(udid)) {
                // Attempt get identifier from queried udid
                client.UDID = udid;
            }

            // If autoPair is true then attempt to pair with the device otherwise just check the status of pairing.
            if (autoPair) {
                client.PairDevice(-1);
            }
            else {
                client.ValidatePairing();
            }

            return client;
        }
    }
}

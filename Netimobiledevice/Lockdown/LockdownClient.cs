using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Netimobiledevice.Exceptions;
using Netimobiledevice.HelperFiles;
using Netimobiledevice.Lockdown.Pairing;
using Netimobiledevice.NotificationProxy;
using Netimobiledevice.Plist;
using Netimobiledevice.Usbmuxd;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Lockdown
{
    public class LockdownClient : LockdownServiceProvider, IDisposable
    {
        public const string DEFAULT_CLIENT_NAME = "Netimobiledevice";
        public const ushort SERVICE_PORT = 62078;
        public const string SYSTEM_BUID = "30142955-444094379208051516";

        private DictionaryNode _allValues;
        private byte[] _devicePublicKey;
        private string _hostId;
        private readonly Version _iosVersion;
        /// <summary>
        /// User agent to use when identifying for lockdownd
        /// </summary>
        private readonly string _label;
        /// <summary>
        /// The internal logger
        /// </summary>
        private readonly ILogger _logger;
        private readonly ConnectionMedium _medium;
        private readonly DirectoryInfo? _pairingRecordsCacheDirectory;
        /// <summary>
        /// The pairing record for the connected device
        /// </summary>
        private DictionaryNode? _pairRecord;
        private ushort _port;
        private string _sessionId;
        private string _systemBuid;
        private readonly UsbmuxdConnectionType _usbmuxdConnectionType;

        protected readonly ServiceConnection? _service;

        public string DeviceClass { get; private set; } = LockdownDeviceClass.UNKNOWN;

        public string DeviceName => GetValue("DeviceName")?.AsStringNode().Value ?? string.Empty;

        public bool EnableWifiConnections {
            get => GetValue("com.apple.mobile.wireless_lockdown", "EnableWifiConnections")?.AsBooleanNode().Value ?? false;
            set => SetValue("com.apple.mobile.wireless_lockdown", "EnableWifiConnections", new BooleanNode(value));
        }

        public string Identifier { get; private set; }

        /// <summary>
        /// Is the connected iOS trusted/paired with this device.
        /// </summary>
        public bool IsPaired { get; private set; }

        public ILogger Logger => _logger;

        public string ProductFriendlyName => ModelIdentifier.GetDeviceModelName(ProductType);

        public string SerialNumber { get; private set; }

        public string UDID { get; private set; }

        public string WifiMacAddress => GetValue("WiFiAddress")?.AsStringNode().Value ?? string.Empty;

        public override Version OsVersion => _iosVersion;

        /// <summary>
        /// Create a LockdownClient instance
        /// </summary>
        /// <param name="service">lockdownd connection handler</param>
        /// <param name="hostId">Used as the host identifier for the handshake</param>
        /// <param name="identifier">Used as an identifier to look for the device pair record</param>
        /// <param name="label">lockdownd user-agent</param>
        /// <param name="systemBuid">System's unique identifier</param>
        /// <param name="pair_record">Use this pair record instead of the default behavior (search in host/create our own)</param>
        /// <param name="pairingRecordsCacheDirectory">Use the following location to search and save pair records</param>
        /// <param name="port">lockdownd service port</param>
        protected LockdownClient(ServiceConnection service, string hostId, string identifier = "", string label = DEFAULT_CLIENT_NAME, string systemBuid = SYSTEM_BUID,
            DictionaryNode? pairRecord = null, DirectoryInfo? pairingRecordsCacheDirectory = null, ushort port = SERVICE_PORT, ILogger? logger = null) : base()
        {
            _logger = logger ?? NullLogger.Instance;
            _service = service;
            Identifier = identifier;
            _label = label;
            _hostId = hostId;
            _systemBuid = systemBuid;
            _pairRecord = pairRecord;
            IsPaired = false;
            _sessionId = string.Empty;
            _pairingRecordsCacheDirectory = pairingRecordsCacheDirectory;
            _port = port;

            if (QueryType() != "com.apple.mobile.lockdown") {
                throw new IncorrectModeException();
            }

            _allValues = GetValue()?.AsDictionaryNode() ?? new DictionaryNode();
            UDID = _allValues["UniqueDeviceID"].AsStringNode().Value;
            _devicePublicKey = _allValues["DevicePublicKey"].AsDataNode().Value;
            ProductType = _allValues["ProductType"].AsStringNode().Value;
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
                _logger.LogWarning(ex, "Warning unauthorised access excpetion when trying to access itunes plist");
            }
            return null;
        }

        private DictionaryNode? GetLocalPairingRecord()
        {
            _logger.LogDebug("Looking for Netimobiledevice pairing record");
            string filePath = $"{UDID}.plist";
            if (_pairingRecordsCacheDirectory != null) {
                filePath = Path.Combine(_pairingRecordsCacheDirectory.FullName, filePath);
            }

            if (File.Exists(filePath)) {
                using (FileStream fs = File.OpenRead(filePath)) {
                    return PropertyList.Load(fs).AsDictionaryNode();
                }
            }
            else {
                _logger.LogDebug($"No Netimobiledevice pairing record found for device {UDID}");
                return null;
            }
        }

        private PropertyNode GetServiceConnectionAttributes(string name, bool useEscrowBag, bool useTrustedConnection)
        {
            if (!IsPaired && useTrustedConnection) {
                throw new NotPairedException();
            }

            DictionaryNode options = new DictionaryNode {
                { "Service", new StringNode(name) }
            };
            if (useEscrowBag && _pairRecord != null) {
                options.Add("EscrowBag", _pairRecord["EscrowBag"]);
            }

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
            if (_pairRecord != null) {
                // If we already have on then use that
                return;
            }

            // First look for an iTunes pair record
            _pairRecord = GetItunesPairingRecord();
            if (_pairRecord != null) {
                _logger.LogDebug("Using iTunes pair record");
                return;
            }

            // Second look for the usbmuxd pair record
            UsbmuxConnection mux = UsbmuxConnection.Create(logger: _logger);
            if (_medium == ConnectionMedium.USBMUX && mux is PlistMuxConnection plistMuxConnection) {
                _pairRecord = plistMuxConnection.GetPairRecord(UDID);
            }
            mux.Close();
            if (_pairRecord != null) {
                _logger.LogDebug($"Using usbmuxd pair record for identifier: {UDID}");
                return;
            }

            // Lastly look for a local pair record
            _pairRecord = GetLocalPairingRecord();
            if (_pairRecord != null) {
                _logger.LogDebug($"Using local pair record: {UDID}.plist");
            }
        }

        private PropertyNode Request(string request, DictionaryNode? options = null, bool verifyRequest = true)
        {
            DictionaryNode message = new DictionaryNode {
                { "Label", new StringNode(_label) },
                { "Request", new StringNode(request) }
            };
            if (options != null) {
                foreach (KeyValuePair<string, PropertyNode> option in options) {
                    message.Add(option);
                }
            }

            DictionaryNode response = _service?.SendReceivePlist(message)?.AsDictionaryNode() ?? new DictionaryNode();

            if (verifyRequest && response["Request"].AsStringNode().Value != request) {
                throw new LockdownException($"Incorrect response returned, as got {response["Request"].AsStringNode().Value} instead of {request}");
            }

            if (response.TryGetValue("Error", out PropertyNode? errorNode)) {
                string error = errorNode.AsStringNode().Value;
                Enum.TryParse(typeof(LockdownError), error, out object? lockdownError);
                throw ((LockdownError?) lockdownError)?.GetException() ?? new LockdownException(error);
            }

            // On iOS < 5: "Error" doesn't exist, so we have to check for "Result" instead
            if (response.TryGetValue("Result", out PropertyNode? resultNode)) {
                string error = resultNode.AsStringNode().Value;
                if (error == "Failure") {
                    throw new LockdownException();
                }
            }

            return response;
        }

        private DictionaryNode RequestPair(DictionaryNode pairOptions)
        {
            try {
                return Request("Pair", pairOptions).AsDictionaryNode();
            }
            catch (LockdownException ex) {
                if (ex.LockdownError == LockdownError.PairingDialogResponsePending) {
                    _logger.LogDebug("Waiting for user pairing dialog...");
                }
                throw;
            }
        }

        private LockdownError Pair()
        {
            _devicePublicKey = GetValue(null, "DevicePublicKey")?.AsDataNode().Value ?? Array.Empty<byte>();
            if (_devicePublicKey == null || _devicePublicKey.Length == 0) {
                _logger.LogDebug("Unable to retrieve DevicePublicKey");
                _service?.Close();
                throw new FatalPairingException();
            }

            _logger.LogDebug("Creating host key & certificate");
            (byte[] rootCertPem, byte[] privateKeyPem, byte[] deviceCertPem) = CertificateGenerator.GeneratePairingCertificates(_devicePublicKey);

            DictionaryNode newPairRecord = new DictionaryNode {
                { "DevicePublicKey", new DataNode(_devicePublicKey) },
                { "DeviceCertificate", new DataNode(deviceCertPem) },
                { "HostCertificate", new DataNode(rootCertPem) },
                { "HostID", new StringNode(_hostId) },
                { "RootCertificate", new DataNode(rootCertPem) },
                { "WiFiMACAddress", new StringNode(WifiMacAddress) },
                { "SystemBUID", new StringNode(_systemBuid) }
            };

            DictionaryNode pairOptions = new DictionaryNode {
                { "PairRecord", newPairRecord },
                { "ProtocolVersion", new StringNode("2") },
                { "PairingOptions", new DictionaryNode() {
                        { "ExtendedPairingErrors", new BooleanNode(true) }
                    }
                }
            };

            DictionaryNode pair;
            try {
                pair = RequestPair(pairOptions);
            }
            catch (LockdownException ex) {
                return ex.LockdownError;
            }

            newPairRecord.Add("HostPrivateKey", new DataNode(privateKeyPem));
            newPairRecord.Add("RootPrivateKey", new DataNode(privateKeyPem));
            if (pair.ContainsKey("EscrowBag")) {
                newPairRecord.Add("EscrowBag", pair["EscrowBag"]);
            }

            _pairRecord = newPairRecord;
            WriteStorageFile($"{UDID}.plist", PropertyList.SaveAsByteArray(_pairRecord, PlistFormat.Xml));

            if (_medium == ConnectionMedium.USBMUX) {
                byte[] recordData = PropertyList.SaveAsByteArray(_pairRecord, PlistFormat.Xml);

                UsbmuxConnection mux = UsbmuxConnection.Create(logger: Logger);
                if (mux is PlistMuxConnection plistMuxConnection) {
                    int deviceId = (int) (_service?.MuxDevice?.DeviceId ?? 0);
                    plistMuxConnection.SavePairRecord(UDID, deviceId, recordData);
                }
                mux.Close();
            }

            IsPaired = true;
            return LockdownError.Success;
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

            if (_pairRecord == null) {
                return false;
            }

            if (_iosVersion < new Version("7.0") && DeviceClass != LockdownDeviceClass.WATCH) {
                try {
                    DictionaryNode options = new DictionaryNode {
                        { "PairRecord", _pairRecord }
                    };
                    Request("ValidatePair", options);
                }
                catch (Exception) {
                    return false;
                }
            }

            _hostId = _pairRecord["HostID"].AsStringNode().Value;
            _systemBuid = _pairRecord["SystemBUID"].AsStringNode().Value;

            DictionaryNode startSession;
            try {
                DictionaryNode options = new DictionaryNode {
                    { "HostID", new StringNode(_hostId) },
                    { "SystemBUID", new StringNode(_systemBuid) }
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

            if (startSession.ContainsKey("EnableSessionSSL") && startSession["EnableSessionSSL"].AsBooleanNode().Value) {
                _service?.StartSSL(_pairRecord["HostCertificate"].AsDataNode().Value, _pairRecord["HostPrivateKey"].AsDataNode().Value);
            }

            IsPaired = true;
            return IsPaired;
        }

        private void WriteStorageFile(string filename, byte[] data)
        {
            if (_pairingRecordsCacheDirectory != null) {
                string file = Path.Combine(_pairingRecordsCacheDirectory.FullName, filename);
                File.WriteAllBytes(file, data);
            }
        }

        protected void HandleAutoPair(bool autoPair, float timeout)
        {
            /* TODO
        if self.validate_pairing():
            return

        # device is not paired yet
        if not autopair:
            # but pairing by default was not requested
            return
        self.pair(timeout=timeout)
        # get session_id
        if not self.validate_pairing():
            raise FatalPairingError() 
            */
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
        public static LockdownClient Create(ServiceConnection service, string identifier = "", string systemBuid = SYSTEM_BUID, string label = DEFAULT_CLIENT_NAME,
            bool autopair = true, float? pairTimeout = null, string localHostname = "", DictionaryNode? pairRecord = null, string pairingRecordsCacheFolder = "",
            ushort port = SERVICE_PORT, ILogger? logger = null)
        {
            string hostId = PairRecords.GenerateHostId(localHostname);
            DirectoryInfo pairingRecordsCacheDirectory = PairRecords.CreatePairingRecordsCacheFolder(pairingRecordsCacheFolder);

            LockdownClient lockdownClient = new(service, hostId: hostId, identifier: identifier, label: label, systemBuid: systemBuid, pairRecord: pairRecord,
                pairingRecordsCacheDirectory: pairingRecordsCacheDirectory, port: port, logger: logger);

            lockdownClient.HandleAutoPair(autopair, pairTimeout ?? -1);
            return lockdownClient;
        }


        public void Dispose()
        {
            _service?.Dispose();
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
        /// Start a pairing operation.
        /// </summary>
        /// <returns>Return <see langword="true"/> if the user accept pairng else <see langword="false"/>.</returns>
        public Task<bool> PairAsync()
        {
            return PairAsync(new Progress<PairingState>(), CancellationToken.None);
        }

        /// <summary>
        /// Start a pairing operation.
        /// </summary>
        /// <param name="cancellationToken">A cancelation token used to cancel stop the operation</param>
        /// <returns>Return <see langword="true"/> if the user accept pairng else <see langword="false"/>.</returns>
        public Task<bool> PairAsync(CancellationToken cancellationToken)
        {
            return PairAsync(new Progress<PairingState>(), cancellationToken);
        }

        /// <summary>
        /// Start a pairing operation.
        /// </summary>
        /// <param name="progress">Used to report the progress</param>
        /// <returns>Return <see langword="true"/> if the user accept pairng else <see langword="false"/>.</returns>
        public Task<bool> PairAsync(IProgress<PairingState> progress)
        {
            return PairAsync(progress, CancellationToken.None);
        }

        /// <summary>
        /// Start a pairing operation.
        /// </summary>
        /// <param name="progress">Used to report the progress</param>
        /// <param name="cancellationToken">A cancelation token used to cancel stop the operation</param>
        /// <returns>Return <see langword="true"/> if the user accept pairng else <see langword="false"/>.</returns>
        public async Task<bool> PairAsync(IProgress<PairingState> progress, CancellationToken cancellationToken)
        {
            using (NotificationProxyService np = new NotificationProxyService(this, true)) {
                np.ObserveNotification(ReceivableNotification.RequestPair);
                LockdownError? err = null;
                PairingState? lastPairingReport = null;
                while (!cancellationToken.IsCancellationRequested) {
                    err = Pair();
                    switch (err) {
                        case LockdownError.Success: {
                            np.Stop();
                            progress.Report(PairingState.Paired);
                            return IsPaired = true;
                        }
                        case LockdownError.UserDeniedPairing: {
                            progress.Report(PairingState.UserDeniedPairing);
                            return IsPaired = false;
                        }
                        case LockdownError.PasswordProtected: {
                            if (lastPairingReport != PairingState.PasswordProtected) {
                                progress.Report(PairingState.PasswordProtected);
                                lastPairingReport = PairingState.PasswordProtected;
                            }
                            break;
                        }
                        case LockdownError.PairingDialogResponsePending: {
                            if (lastPairingReport != PairingState.PairingDialogResponsePending) {
                                progress.Report(PairingState.PairingDialogResponsePending);
                                lastPairingReport = PairingState.PairingDialogResponsePending;
                            }
                            break;
                        }
                        default: {
                            IsPaired = false;
                            throw ((LockdownError) err).GetException() ?? new LockdownException(LockdownError.UnknownError);
                        }
                    }
                    await Task.Delay(200, cancellationToken).ConfigureAwait(false);
                }
            }

            if (IsPaired) {
                ValidatePairing();
            }
            return IsPaired;
        }

        /// <summary>
        /// Attempts to pair with the currently connected iOS device, or returns true if the device is already paired.
        /// </summary>
        /// <param name="timeout">How long to wait when pairing the iOS device</param>
        /// <returns>If the device is currently paired or if the pairing was successful or not</returns>
        /// <exception cref="FatalPairingException">Exception thrown when pairing should have succeeded but failed for some reason.</exception>
        public bool PairDevice()
        {
            bool currentlyPaired = ValidatePairing();
            if (currentlyPaired) {
                return true;
            }

            // The device is not paired so we attempt to pair it.
            Pair();

            // Get sessionId
            if (ValidatePairing()) {
                throw new FatalPairingException();
            }

            // Now we are paied, reload data
            _allValues = GetValue()?.AsDictionaryNode() ?? new DictionaryNode();
            UDID = _allValues["UniqueDeviceID"].AsStringNode().Value;
            return IsPaired;
        }

        public virtual void SavePairRecord()
        {
            /* TODO
        pair_record_file = self.pairing_records_cache_folder / f'{self.identifier}.plist'
        pair_record_file.write_bytes(plistlib.dumps(self.pair_record))
            */
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

        /// <summary>
        /// Used to establish a new ServiceConnection to a given port
        /// </summary>
        /// <param name="port"></param>
        /// <returns></returns>
        protected virtual ServiceConnection CreateServiceConnection(ushort port)
        {
            throw new NotImplementedException();
        }

        public override ServiceConnection StartLockdownService(string name, bool useEscrowBag = false, bool useTrustedConnection = true)
        {
            DictionaryNode attr = GetServiceConnectionAttributes(name, useEscrowBag, useTrustedConnection).AsDictionaryNode();
            ServiceConnection serviceConnection = CreateServiceConnection((ushort) attr["Port"].AsIntegerNode().Value);

            if (attr.TryGetValue("EnableServiceSSL", out PropertyNode? enableServiceSsl) && enableServiceSsl?.AsBooleanNode().Value == true) {
                if (_pairRecord == null) {
                    throw new FatalPairingException("Pair Record is null when it shouldn't be");
                }
                serviceConnection.StartSSL(_pairRecord["HostCertificate"].AsDataNode().Value, _pairRecord["HostPrivateKey"].AsDataNode().Value);
            }
            return serviceConnection;
        }

        /// <summary>
        /// Try to unpair the device.
        /// </summary>
        public void Unpair()
        {
            if (_pairRecord != null) {
                DictionaryNode options = new DictionaryNode() {
                    { "PairRecord", _pairRecord },
                    { "ProtocolVersion", new StringNode("2")}
                };
                Request("Unpair", options, true);
                IsPaired = false;
                _pairRecord = null;
            }
        }
    }
}

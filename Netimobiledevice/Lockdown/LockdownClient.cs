using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Netimobiledevice.Exceptions;
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
    public abstract class LockdownClient : LockdownServiceProvider, IDisposable
    {
        public const string DEFAULT_CLIENT_NAME = "Netimobiledevice";
        public const ushort SERVICE_PORT = 62078;
        public const string SYSTEM_BUID = "30142955-444094379208051516";

        private DictionaryNode _allValues;
        private byte[] _devicePublicKey;
        private string _hostId;
        /// <summary>
        /// User agent to use when identifying for lockdownd
        /// </summary>
        private readonly string _label;
        /// <summary>
        /// The internal logger
        /// </summary>
        private readonly ILogger _logger;
        private readonly ConnectionMedium _medium;
        private readonly ushort _port;
        private readonly string _sessionId;
        private string _systemBuid;

        protected readonly DirectoryInfo? _pairingRecordsCacheDirectory;
        /// <summary>
        /// The pairing record for the connected device
        /// </summary>
        protected DictionaryNode? _pairRecord;
        protected readonly ServiceConnection? _service;

        public UsbmuxdConnectionType ConnectionType { get; protected set; }

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

        public override ILogger Logger => _logger;

        public string ProductFriendlyName => ModelIdentifier.GetDeviceModelName(ProductType);

        public string SerialNumber { get; private set; } = string.Empty;

        public string WifiMacAddress => GetValue("WiFiAddress")?.AsStringNode().Value ?? string.Empty;

        public override Version OsVersion => Version.Parse(_allValues["ProductVersion"].AsStringNode().Value);

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
        /// <param name="logger"></param>
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

            _allValues = GetValue()?.AsDictionaryNode() ?? [];

            if (_allValues.TryGetValue("UniqueDeviceID", out PropertyNode? UdidNode)) {
                Udid = UdidNode.AsStringNode().Value;
            }
            ProductType = _allValues["ProductType"].AsStringNode().Value;

            if (_allValues.TryGetValue("DevicePublicKey", out PropertyNode? devicePublicKeyNode)) {
                _devicePublicKey = devicePublicKeyNode.AsDataNode().Value;
            }
            else {
                _devicePublicKey = [];
            }
        }

        private DictionaryNode GetServiceConnectionAttributes(string name, bool useEscrowBag, bool useTrustedConnection)
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

        private DictionaryNode Request(string request, DictionaryNode? options = null, bool verifyRequest = true)
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

            DictionaryNode response = _service?.SendReceivePlist(message)?.AsDictionaryNode() ?? [];

            if (verifyRequest && response.TryGetValue("Request", out PropertyNode? requestNode)) {
                if (requestNode.AsStringNode().Value != request) {
                    Logger.LogWarning("Request response did not contain our expected value {value}: {response}", requestNode, response);
                    throw new LockdownException($"Incorrect response returned, as got {requestNode} instead of {request}");
                }
            }
            else if (verifyRequest) {
                throw new LockdownException("Response did not contain the key \"Request\"");
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
            _devicePublicKey = GetValue(null, "DevicePublicKey")?.AsDataNode().Value ?? [];
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
            WriteStorageFile($"{Udid}.plist", PropertyList.SaveAsByteArray(_pairRecord, PlistFormat.Xml));

            if (_medium == ConnectionMedium.USBMUX) {
                byte[] recordData = PropertyList.SaveAsByteArray(_pairRecord, PlistFormat.Xml);

                UsbmuxConnection mux = UsbmuxConnection.Create(logger: Logger);
                if (mux is PlistMuxConnection plistMuxConnection) {
                    long deviceId = _service?.MuxDevice?.DeviceId ?? -1;
                    plistMuxConnection.SavePairRecord(Udid, deviceId, recordData);
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
            if (_pairRecord == null && !string.IsNullOrEmpty(Identifier)) {
                try {
                    FetchPairRecord();
                }
                catch (NotPairedException) {
                    IsPaired = false;
                    return IsPaired;
                }
            }

            if (_pairRecord == null) {
                IsPaired = false;
                return IsPaired;
            }

            if (OsVersion < new Version("7.0") && DeviceClass != LockdownDeviceClass.WATCH) {
                try {
                    Request("ValidatePair", new DictionaryNode { { "PairRecord", _pairRecord } });
                }
                catch (Exception) {
                    IsPaired = false;
                    return IsPaired;
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
                    IsPaired = false;
                    return IsPaired;
                }
                else {
                    throw;
                }
            }

            if (startSession.ContainsKey("EnableSessionSSL") && startSession["EnableSessionSSL"].AsBooleanNode().Value) {
                _service?.StartSSL(_pairRecord["HostCertificate"].AsDataNode().Value, _pairRecord["HostPrivateKey"].AsDataNode().Value);
            }

            IsPaired = true;

            // Reload data after pairing
            _allValues = GetValue()?.AsDictionaryNode() ?? [];
            Udid = _allValues["UniqueDeviceID"].AsStringNode().Value;

            return IsPaired;
        }

        private void WriteStorageFile(string filename, byte[] data)
        {
            if (_pairingRecordsCacheDirectory != null) {
                if (!_pairingRecordsCacheDirectory.Exists) {
                    _pairingRecordsCacheDirectory.Create();
                }
                string file = Path.Combine(_pairingRecordsCacheDirectory.FullName, filename);
                File.WriteAllBytes(file, data);
            }
        }

        protected virtual void HandleAutoPair(bool autoPair, float timeout)
        {
            if (ValidatePairing()) {
                return;
            }

            // The device is not paired yet
            if (!autoPair) {
                // pairing automatically was not requested
                return;
            }

            PairDevice();

            if (!ValidatePairing()) {
                throw new FatalPairingException();
            }
        }

        protected virtual void FetchPairRecord()
        {
            _pairRecord = PairRecords.GetPreferredPairRecord(Identifier, _pairingRecordsCacheDirectory, logger: Logger);
        }

        public void Close()
        {
            Dispose();
        }

        public void Dispose()
        {
            _service?.Dispose();
            GC.SuppressFinalize(this);
        }

        public override PropertyNode? GetValue(string? domain, string? key)
        {
            DictionaryNode options = [];
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
        /// Start a pairing operation.
        /// </summary>
        /// <returns>Return <see langword="true"/> if the user accept pairng else <see langword="false"/>.</returns>
        public virtual Task<bool> PairAsync()
        {
            return PairAsync(new Progress<PairingState>(), CancellationToken.None);
        }

        /// <summary>
        /// Start a pairing operation.
        /// </summary>
        /// <param name="cancellationToken">A cancelation token used to cancel stop the operation</param>
        /// <returns>Return <see langword="true"/> if the user accept pairng else <see langword="false"/>.</returns>
        public virtual Task<bool> PairAsync(CancellationToken cancellationToken)
        {
            return PairAsync(new Progress<PairingState>(), cancellationToken);
        }

        /// <summary>
        /// Start a pairing operation.
        /// </summary>
        /// <param name="progress">Used to report the progress</param>
        /// <returns>Return <see langword="true"/> if the user accept pairng else <see langword="false"/>.</returns>
        public virtual Task<bool> PairAsync(IProgress<PairingState> progress)
        {
            return PairAsync(progress, CancellationToken.None);
        }

        /// <summary>
        /// Start a pairing operation.
        /// </summary>
        /// <param name="progress">Used to report the progress</param>
        /// <param name="cancellationToken">A cancelation token used to cancel stop the operation</param>
        /// <returns>Return <see langword="true"/> if the user accept pairng else <see langword="false"/>.</returns>
        public virtual async Task<bool> PairAsync(IProgress<PairingState> progress, CancellationToken cancellationToken)
        {
            using (NotificationProxyService np = new NotificationProxyService(this, true)) {
                await np.ObserveNotificationAsync(ReceivableNotification.RequestPair).ConfigureAwait(false);

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
        public virtual bool PairDevice()
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
            _allValues = GetValue()?.AsDictionaryNode() ?? [];
            Udid = _allValues["UniqueDeviceID"].AsStringNode().Value;
            return IsPaired;
        }

        public virtual void SavePairRecord()
        {
            if (_pairingRecordsCacheDirectory != null) {
                string pairRecordFilePath = Path.Combine(_pairingRecordsCacheDirectory.FullName, $"{Identifier}.plist");
                if (_pairRecord != null) {
                    File.WriteAllBytes(pairRecordFilePath, PropertyList.SaveAsByteArray(_pairRecord, PlistFormat.Xml));
                }
            }
        }

        public PropertyNode SetValue(string? domain, string? key, PropertyNode value)
        {
            DictionaryNode options = [];
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
        public virtual Task<ServiceConnection> CreateServiceConnection(ushort port)
        {
            throw new NotImplementedException();
        }

        public override async Task<ServiceConnection> StartLockdownService(string name, bool useEscrowBag = false, bool useTrustedConnection = true)
        {
            DictionaryNode attr = GetServiceConnectionAttributes(name, useEscrowBag, useTrustedConnection).AsDictionaryNode();
            ServiceConnection serviceConnection = await CreateServiceConnection((ushort) attr["Port"].AsIntegerNode().Value).ConfigureAwait(false);

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
        public virtual void Unpair()
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

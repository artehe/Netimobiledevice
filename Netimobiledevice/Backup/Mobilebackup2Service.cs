using Netimobiledevice.EndianBitConversion;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Lockdown.Services;
using Netimobiledevice.Plist;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace Netimobiledevice.Backup
{
    public sealed class Mobilebackup2Service : BaseService
    {
        private const string SERVICE_NAME = "com.apple.mobilebackup2";

        private DeviceLink? deviceLink;

        protected override string ServiceName => SERVICE_NAME;

        private Mobilebackup2Service(LockdownClient client) : base(client, GetServiceConnection(client)) { }

        private void ChangeBackupEncryptionPassword(string? oldPassword, string? newPassword, BackupEncryptionFlags flag)
        {
            if (newPassword == null && oldPassword == null) {
                throw new Exception("Both newPassword and oldPassword can't be null");
            }

            DictionaryNode opts = new DictionaryNode {
                { "TargetIdentifier", new StringNode(Lockdown.UDID) }
            };

            DictionaryNode backupDomain = Lockdown.GetValue("com.apple.mobile.backup", null)?.AsDictionaryNode() ?? new DictionaryNode();
            if (flag == BackupEncryptionFlags.ChangePassword && backupDomain.ContainsKey("WillEncrypt") && !backupDomain["WillEncrypt"].AsBooleanNode().Value) {
                Debug.WriteLine("Error Backup encryption is not enabled so can't change password. Aborting");
                throw new InvalidOperationException("Backup encryption isn't enabled so can't change password");
            }

            if (newPassword != null) {
                opts.Add("NewPassword", new StringNode(newPassword));
            }
            if (oldPassword != null) {
                opts.Add("OldPassword", new StringNode(oldPassword));
            }

            SendMessage("ChangePassword", opts);
        }

        private async Task<DeviceLink> GetDeviceLink()
        {
            DeviceLink deviceLink = new DeviceLink(Service);
            await deviceLink.VersionExchange();
            await VersionExchange(deviceLink);
            return deviceLink;
        }

        private static ServiceConnection GetServiceConnection(LockdownClient client)
        {
            return client.StartService(SERVICE_NAME, useEscrowBag: true);
        }

        private async Task<Mobilebackup2Service> InitializeAsync()
        {
            deviceLink = await GetDeviceLink();
            return this;
        }

        private void SendMessage(string message, DictionaryNode options)
        {
            if (string.IsNullOrEmpty(message) && options == null) {
                throw new ArgumentException("Argument(s) can't be null or empty");
            }

            if (!string.IsNullOrEmpty(message)) {
                DictionaryNode dict = new DictionaryNode();
                if (options != null) {
                    dict = options;
                }
                dict.Add("MessageName", new StringNode(message));

                // Send it as DLMessageProcessMessage 
                deviceLink?.SendProcessMessage(dict);
            }
            else {
                deviceLink?.SendProcessMessage(options);
            }
        }

        private void SendPrefixed(byte[] data, int length)
        {
            Service.Send(EndianBitConverter.BigEndian.GetBytes(length));
            Service.Send(data);
        }

        /// <summary>
        /// Exchange versions with the device and assert that the device supports our version of the protocol.
        /// </summary>
        /// <param name="deviceLink">Initialized device link.</param>
        private static async Task VersionExchange(DeviceLink deviceLink)
        {
            ArrayNode supportedVersions = new ArrayNode {
                new RealNode(2.0),
                new RealNode(2.1)
            };
            deviceLink.SendProcessMessage(new DictionaryNode() {
                {"MessageName", new StringNode("Hello") },
                {"SupportedProtocolVersions", supportedVersions }
            });

            ArrayNode reply = await deviceLink.ReceiveMessage();
            if (reply[0].AsStringNode().Value != "DLMessageProcessMessage" || reply[1].AsDictionaryNode()["ErrorCode"].AsIntegerNode().Value != 0) {
                throw new Exception($"Found error in response during version exchange");
            }
            if (!supportedVersions.Contains(reply[1].AsDictionaryNode()["ProtocolVersion"])) {
                throw new Exception("Unsuppored protocol version found");
            }
        }

        /// <summary>
        /// Change the backup password on the current device.
        /// </summary>
        /// <param name="oldPassword">The current password</param>
        /// <param name="newPassword">The password to set the backup to</param>
        public void ChangeBackupEncryptionPassword(string oldPassword, string newPassword)
        {
            ChangeBackupEncryptionPassword(oldPassword, newPassword, BackupEncryptionFlags.ChangePassword);
        }

        public static Task<Mobilebackup2Service> CreateAsync(LockdownClient client)
        {
            Mobilebackup2Service ret = new Mobilebackup2Service(client);
            return ret.InitializeAsync();
        }

        public override void Dispose()
        {
            base.Dispose();
        }

        public async Task<ArrayNode> ReceiveMessage()
        {
            if (deviceLink != null) {
                return await deviceLink.ReceiveMessage();
            }
            throw new NullReferenceException("DeviceLink null, please run LoadDeviceLink function before calling");
        }

        public byte[] ReceiveRaw(int length)
        {
            return Service.Receive(length);
        }

        /// <summary>
        /// Sends the specified error report to the backup service.
        /// </summary>
        /// <param name="error">The error report to send.</param>
        public void SendError(DictionaryNode errorReport)
        {
            byte[] errBytes = Encoding.UTF8.GetBytes(errorReport["DLFileErrorString"].AsStringNode().Value);
            List<byte> buffer = new List<byte> {
                (byte) ResultCode.LocalError
            };
            buffer.AddRange(errBytes);
            SendPrefixed(buffer.ToArray(), buffer.Count);
        }

        public void SendRaw(byte[] data)
        {
            Service.Send(data);
        }

        /// <summary>
        /// Sends a filename to the backup service stream.
        /// </summary>
        /// <param name="filename">The filename to send.</param>
        public void SendPath(string filename)
        {
            byte[] path = Encoding.UTF8.GetBytes(filename);
            SendPrefixed(path, path.Length);
        }

        public void SendRequest(string request, string targetIdentifier, string sourceIdentifier, DictionaryNode options)
        {
            DictionaryNode dict = new DictionaryNode() {
                { "TargetIdentifier", new StringNode(targetIdentifier) }
            };

            if (!string.IsNullOrEmpty(sourceIdentifier)) {
                dict.Add("SourceIdentifier", new StringNode(sourceIdentifier));
            }

            if (options != null) {
                dict.Add("Options", options);
            }

            if (request == "Unback" && options != null) {
                PropertyNode node = options["Password"];
                if (node != null) {
                    dict.Add("Password", node);
                }
            }
            if (request == "EnableCloudBackup" && options != null) {
                PropertyNode node = options["CloudBackupState"];
                if (node != null) {
                    dict.Add("CloudBackupState", node);
                }
            }

            SendMessage(request, dict);
        }

        /// <summary>
        /// Sends a status report to the backup service.
        /// </summary>
        /// <param name="errorCode">The error code to send (as errno value).</param>
        /// <param name="errorMessage">The error message to send.</param>
        /// <param name="errorList">A PropertyNode with additional value(s).</param>
        public void SendStatusReport(int errorCode, string? errorMessage, PropertyNode? errorList)
        {
            ArrayNode array = new ArrayNode {
                new StringNode("DLMessageStatusResponse"),
                new IntegerNode(errorCode)
            };

            if (errorMessage != null) {
                array.Add(new StringNode(errorMessage));
            }
            else {
                array.Add(new StringNode("___EmptyParameterString___"));
            }

            if (errorList != null) {
                array.Add(errorList);
            }
            else {
                array.Add(new DictionaryNode());
            }

            deviceLink?.Send(array);
        }

        /// <summary>
        /// Sends a status report to the backup service.
        /// </summary>
        /// <param name="errorCode">The error code to send (as errno value).</param>
        /// <param name="errorMessage">The error message to send.</param>
        public void SendStatusReport(int errorCode, string errorMessage)
        {
            SendStatusReport(errorCode, errorMessage, null);
        }

        /// <summary>
        /// Enables encrypted backups by setting a password for backups to use provided
        /// the phone currently has encrypted backups disabled. 
        /// </summary>
        /// <param name="password"></param>
        public void SetBackupPassword(string password)
        {
            DictionaryNode backupDomain = Lockdown.GetValue("com.apple.mobile.backup", null)?.AsDictionaryNode() ?? new DictionaryNode();
            if (backupDomain.ContainsKey("WillEncrypt") && backupDomain["WillEncrypt"].AsBooleanNode().Value) {
                Debug.WriteLine("ERROR Backup encryption is already enabled. Aborting.");
                throw new InvalidOperationException("Can't set backup password as one already exists");
            }

            if (password == null || password == string.Empty) {
                Debug.WriteLine("No backup password given. Aborting.");
                throw new ArgumentException("password can't be null or empty");
            }

            ChangeBackupEncryptionPassword(null, password, BackupEncryptionFlags.Enable);
        }
    }
}

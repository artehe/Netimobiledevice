using Microsoft.Extensions.Logging;
using Netimobiledevice.Backup;
using Netimobiledevice.EndianBitConversion;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Lockdown.Services;
using Netimobiledevice.Plist;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.DeviceLink
{
    public abstract class DeviceLinkService : BaseService
    {
        // Set the default timeout to be 5 minutes
        private const int SERVICE_TIMEOUT = 5 * 60 * 1000;

        private const int BULK_OPERATION_ERROR = -13;
        private const UInt32 FILE_TRANSFER_TERMINATOR = 0x00;
        private const byte CODE_FILE_DATA = 0xC;
        private const byte CODE_SUCCESS = 0x0;

        protected DeviceLinkService(LockdownClient lockdown, ServiceConnection? service = null) : base(lockdown, service)
        {
            // Adjust the timeout to be long enough to handle device with a large amount of data
            Service.SetTimeout(SERVICE_TIMEOUT);
        }

        /// <summary>
        /// Creates a dictionary plist instance of the required error report for the device.
        /// </summary>
        /// <param name="errorNo">The errno code.</param>
        private static DictionaryNode CreateErrorReport(int errorNo)
        {
            string errMsg;
            int errCode = -errorNo;

            if (errorNo == (int) ErrNo.ENOENT) {
                errCode = -6;
                errMsg = "No such file or directory.";
            }
            else if (errorNo == (int) ErrNo.EEXIST) {
                errCode = -7;
                errMsg = "File or directory already exists.";
            }
            else {
                errMsg = $"Unspecified error: ({errorNo})";
            }

            DictionaryNode dict = new DictionaryNode() {
                { "DLFileErrorString", new StringNode(errMsg) },
                { "DLFileErrorCode", new IntegerNode(errCode) }
            };
            return dict;
        }

        private void Disconnect()
        {
            ArrayNode message = new ArrayNode {
                new StringNode("DLMessageDisconnect"),
                new StringNode("___EmptyParameterString___")
            };
            try {
                Service.SendPlist(message, PlistFormat.Binary);
            }
            catch (ObjectDisposedException) {
                Logger.LogWarning("Trying to send disconnect from disposed service");
            }
        }

        private static double GetProgressForMessage(ArrayNode msg, int index)
        {
            return msg[index].AsRealNode().Value;
        }

        /// <summary>
        /// Sends the specified error report to the backup service.
        /// </summary>
        /// <param name="error">The error report to send.</param>
        public async Task SendError(DictionaryNode errorReport, CancellationToken cancellationToken)
        {
            byte[] errBytes = Encoding.UTF8.GetBytes(errorReport["DLFileErrorString"].AsStringNode().Value);
            List<byte> buffer = new List<byte> {
                (byte) ResultCode.LocalError
            };
            buffer.AddRange(errBytes);
            await SendPrefixed(buffer.ToArray(), buffer.Count, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends a filename to the backup service stream.
        /// </summary>
        /// <param name="filename">The filename to send.</param>
        private async Task SendPath(string filename, CancellationToken cancellationToken)
        {
            byte[] path = Encoding.UTF8.GetBytes(filename);
            await SendPrefixed(path, path.Length, cancellationToken).ConfigureAwait(false);
        }

        private async Task SendPrefixed(byte[] data, int length, CancellationToken cancellationToken)
        {
            await Service.SendAsync(EndianBitConverter.BigEndian.GetBytes(length), cancellationToken).ConfigureAwait(false);
            await Service.SendAsync(data, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Manages the DownloadFiles device message.
        /// </summary>
        /// <param name="msg">The message received from the device.</param>
        private async Task OnDownloadFiles(ArrayNode msg, string rootPath, Action<double>? progressCallback = null, CancellationToken cancellationToken = default)
        {
            progressCallback?.Invoke(GetProgressForMessage(msg, 3));

            DictionaryNode errList = new DictionaryNode();
            ArrayNode files = msg[1].AsArrayNode();
            foreach (StringNode filename in files.Cast<StringNode>()) {
                Logger.LogDebug("Sending file: {filename}", filename);
                if (cancellationToken.IsCancellationRequested) {
                    break;
                }
                await SendPath(filename.Value, cancellationToken).ConfigureAwait(false);

                string filePath = Path.Combine(rootPath, filename.Value);

                int errorCode = 0;
                if (!File.Exists(filePath)) {
                    errorCode = 2;
                }

                using (FileStream fs = File.OpenRead(filePath)) {
                    // We want to use a chunk size of 128 MiB
                    byte[] chunk = new byte[128 * 1024 * 1024];

                    int bytesRead;
                    while ((bytesRead = await fs.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0) {
                        List<byte> data = new List<byte> {
                            CODE_FILE_DATA
                        };
                        data.AddRange(chunk.Take(bytesRead));
                        await SendPrefixed(data.ToArray(), data.Count, cancellationToken).ConfigureAwait(false);
                    }
                }

                if (errorCode == 0) {
                    byte[] buffer = new byte[] { CODE_SUCCESS };
                    await SendPrefixed(buffer, buffer.Length, cancellationToken).ConfigureAwait(false);
                }
                else {
                    Logger.LogDebug("Sending Error Code: {code}", errorCode);
                    DictionaryNode errReport = CreateErrorReport(errorCode);
                    errList.Add(filename.Value, errReport);
                    await SendError(errReport, cancellationToken).ConfigureAwait(false);
                }
            }

            await Service.SendAsync(BitConverter.GetBytes(FILE_TRANSFER_TERMINATOR), cancellationToken).ConfigureAwait(false);
            if (errList.Count == 0) {
                SendStatusReport(0);
            }
            else {
                SendStatusReport(BULK_OPERATION_ERROR, "Multi status", errList);
            }
        }

        protected async Task<ArrayNode> DeviceLinkReceiveMessage(CancellationToken cancellationToken)
        {
            PropertyNode? message = await Service.ReceivePlistAsync(cancellationToken);
            if (message == null) {
                return new ArrayNode();
            }
            return message.AsArrayNode();
        }

        protected void DeviceLinkSend(PropertyNode message)
        {
            Service.SendPlist(message, PlistFormat.Binary);
        }

        /// <summary>
        /// Sends a DLMessagePing plist.
        /// </summary>
        /// <param name="message">String to send as ping message.</param>
        protected void DeviceLinkSendPing(string message)
        {
            ArrayNode msg = new ArrayNode() {
                new StringNode("DLMessagePing"),
                new StringNode(message)
            };
            DeviceLinkSend(msg);
        }

        protected void DeviceLinkSendProcessMessage(PropertyNode message)
        {
            Service.SendPlist(new ArrayNode() {
                new StringNode("DLMessageProcessMessage"),
                message
            }, PlistFormat.Binary);
        }

        /// <summary>
        /// Performs the DLMessageVersionExchange with the connected device. 
        /// This should be the first operation to be executed by an implemented
        /// device link service client.
        /// </summary>
        /// <param name="versionMajor">The major version number to check.</param>
        /// <param name="versionMinor">The minor version number to check.</param>
        protected async Task DeviceLinkVersionExchange(ulong versionMajor, ulong versionMinor, CancellationToken cancellationToken)
        {
            // Get DLMessageVersionExchange from device
            ArrayNode versionExchangeMessage = await DeviceLinkReceiveMessage(cancellationToken);
            string dlMessage = versionExchangeMessage[0].AsStringNode().Value;
            if (string.IsNullOrEmpty(dlMessage) || dlMessage != "DLMessageVersionExchange") {
                throw new DeviceLinkException("Didn't receive DLMessageVersionExchange from device");
            }
            if (versionExchangeMessage.Count < 3) {
                throw new DeviceLinkException("DLMessageVersionExchange has unexpected format");
            }

            // Get major and minor version number
            ulong vMajor = versionExchangeMessage[1].AsIntegerNode().Value;
            ulong vMinor = versionExchangeMessage[2].AsIntegerNode().Value;
            if (vMajor > versionMajor) {
                throw new DeviceLinkException($"Version mismatch detected received {vMajor}.{vMinor}, expected {versionMajor}.{versionMinor}");
            }
            else if (vMajor == versionMajor && vMinor > versionMinor) {
                throw new DeviceLinkException($"Version mismatch detected received {vMajor}.{vMinor}, expected {versionMajor}.{versionMinor}");
            }

            // The version is ok so send reply
            Service.SendPlist(new ArrayNode {
                new StringNode("DLMessageVersionExchange"),
                new StringNode("DLVersionsOk"),
                new IntegerNode(versionMajor)
            }, PlistFormat.Binary);

            // Receive DeviceReady message
            ArrayNode messageDeviceReady = await DeviceLinkReceiveMessage(cancellationToken);
            dlMessage = messageDeviceReady[0].AsStringNode().Value;
            if (string.IsNullOrEmpty(dlMessage) || dlMessage != "DLMessageDeviceReady") {
                throw new DeviceLinkException("Device link didn't return ready state (DLMessageDeviceReady)");
            }
        }

        /// <summary>
        /// Sends a status report to the backup service.
        /// </summary>
        /// <param name="errorCode">The error code to send (as errno value).</param>
        /// <param name="errorMessage">The error message to send.</param>
        /// <param name="errorList">A PropertyNode with additional value(s).</param>
        protected void SendStatusReport(int errorCode, string? errorMessage = null, PropertyNode? errorList = null)
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

            DeviceLinkSend(array);
        }

        public override void Dispose()
        {
            Disconnect();
            Close();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Process the message received from the backup service.
        /// </summary>
        /// <param name="msg">The property array received.</param>
        /// <param name="message">The string that identifies the message type.</param>
        /// <returns>Depends on the message type, but a negative value always indicates an error.</returns>
        public async Task OnDeviceLinkMessageReceived(ArrayNode msg, string message, string rootPath, Action<double>? progressCallback = null, CancellationToken cancellationToken = default)
        {
            Logger.LogDebug("Message Received: {message}", message);
            switch (message) {
                case DeviceLinkMessage.DownloadFiles: {
                    await OnDownloadFiles(msg, rootPath, progressCallback, cancellationToken).ConfigureAwait(false);
                    break;
                }
                case DeviceLinkMessage.GetFreeDiskSpace: {
                    OnGetFreeDiskSpace(msg);
                    break;
                }
                case DeviceLinkMessage.CreateDirectory: {
                    // TODO OnCreateDirectory(msg);
                    break;
                }
                case DeviceLinkMessage.UploadFiles: {
                    // TODO OnUploadFiles(msg);
                    break;
                }
                case DeviceLinkMessage.ContentsOfDirectory: {
                    // TODO OnListDirectory(msg);
                    break;
                }
                case DeviceLinkMessage.MoveFiles:
                case DeviceLinkMessage.MoveItems: {
                    // TODO OnMoveItems(msg);
                    break;
                }
                case DeviceLinkMessage.RemoveFiles:
                case DeviceLinkMessage.RemoveItems: {
                    // TODO OnRemoveItems(msg);
                    break;
                }
                case DeviceLinkMessage.CopyItem: {
                    // TODO OnCopyItem(msg);
                    break;
                }
                case DeviceLinkMessage.Disconnect: {
                    // TODO IsCancelling = true;
                    break;
                }
                case DeviceLinkMessage.ProcessMessage: {
                    // TODO OnProcessMessage(msg);
                    break;
                }
                default: {
                    Logger.LogWarning("Unknown message in MessageLoop: {message}", message);
                    // TODO mobilebackup2Service?.SendStatusReport(1, "Operation not supported");
                    break;
                }
            }
        }
    }
}

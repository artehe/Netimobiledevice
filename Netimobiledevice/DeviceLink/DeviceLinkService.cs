using Microsoft.Extensions.Logging;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Lockdown.Services;
using Netimobiledevice.Plist;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.DeviceLink
{
    public abstract class DeviceLinkService : BaseService
    {
        // Set the default timeout to be 5 minutes
        private const int SERVICE_TIMEOUT = 5 * 60 * 1000;

        protected DeviceLinkService(LockdownClient lockdown, ServiceConnection? service = null) : base(lockdown, service)
        {
            // Adjust the timeout to be long enough to handle device with a large amount of data
            Service.SetTimeout(SERVICE_TIMEOUT);
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
                throw new Exception("Didn't receive DLMessageVersionExchange from device");
            }
            if (versionExchangeMessage.Count < 3) {
                throw new Exception("DLMessageVersionExchange has unexpected format");
            }

            // Get major and minor version number
            ulong vMajor = versionExchangeMessage[1].AsIntegerNode().Value;
            ulong vMinor = versionExchangeMessage[2].AsIntegerNode().Value;
            if (vMajor > versionMajor) {
                throw new Exception($"Version mismatch detected received {vMajor}.{vMinor}, expected {versionMajor}.{versionMinor}");
            }
            else if (vMajor == versionMajor && vMinor > versionMinor) {
                throw new Exception($"Version mismatch detected received {vMajor}.{vMinor}, expected {versionMajor}.{versionMinor}");
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
                throw new Exception("Device link didn't return ready state (DLMessageDeviceReady)");
            }
        }

        public override void Dispose()
        {
            Disconnect();
            Close();
            GC.SuppressFinalize(this);
        }
    }
}

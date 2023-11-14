using Netimobiledevice.Plist;
using System;
using System.Threading.Tasks;

namespace Netimobiledevice.Lockdown.Services
{
    internal sealed class DeviceLink : IDisposable
    {
        private const int SERVICE_TIMEOUT = 300 * 1000;

        private readonly ServiceConnection _service;

        public DeviceLink(ServiceConnection service)
        {
            _service = service;
            // Adjust the timeout to be long enough to handle device with a large amount of data
            _service.SetTimeout(SERVICE_TIMEOUT);
        }

        private void Disconnect()
        {
            ArrayNode message = new ArrayNode {
                new StringNode("DLMessageDisconnect"),
                new StringNode("___EmptyParameterString___")
            };
            _service.SendPlist(message, PlistFormat.Binary);
        }

        public void Dispose()
        {
            Disconnect();
        }

        public async Task<ArrayNode> ReceiveMessage()
        {
            PropertyNode? message = await _service.ReceivePlistAsync();
            if (message == null) {
                return new ArrayNode();
            }
            return message.AsArrayNode();
        }

        public void Send(PropertyNode message)
        {
            _service.SendPlist(message, PlistFormat.Binary);
        }

        public void SendProcessMessage(PropertyNode message)
        {
            _service.SendPlist(new ArrayNode() {
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
        public async Task VersionExchange(ulong versionMajor, ulong versionMinor)
        {
            // Get DLMessageVersionExchange from device
            ArrayNode versionExchangeMessage = await ReceiveMessage();
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
            else if ((vMajor == versionMajor) && (vMinor > versionMinor)) {
                throw new Exception($"Version mismatch detected received {vMajor}.{vMinor}, expected {versionMajor}.{versionMinor}");
            }

            // The version is ok so send reply
            _service.SendPlist(new ArrayNode {
                new StringNode("DLMessageVersionExchange"),
                new StringNode("DLVersionsOk"),
                new IntegerNode(versionMajor)
            }, PlistFormat.Binary);

            // Receive DeviceReady message
            ArrayNode messageDeviceReady = await ReceiveMessage();
            dlMessage = messageDeviceReady[0].AsStringNode().Value;
            if (string.IsNullOrEmpty(dlMessage) || dlMessage != "DLMessageDeviceReady") {
                throw new Exception("Device link didn't return ready state (DLMessageDeviceReady)");
            }
        }
    }
}

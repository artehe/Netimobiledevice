﻿using Netimobiledevice.Lockdown;
using Netimobiledevice.Plist;
using System;
using System.Threading.Tasks;

namespace Netimobiledevice.Backup
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

        public async Task VersionExchange()
        {
            ArrayNode versionExchangeMessage = await ReceiveMessage();
            PropertyNode versionMajor = versionExchangeMessage[1];
            _service.SendPlist(new ArrayNode {
                new StringNode("DLMessageVersionExchange"),
                new StringNode("DLVersionsOk"),
                versionMajor
            });
            ArrayNode messageDeviceReady = await ReceiveMessage();
            if (messageDeviceReady[0].AsStringNode().Value != "DLMessageDeviceReady") {
                throw new Exception("Device link didn't return ready state");
            }
        }
    }
}
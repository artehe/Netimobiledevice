using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Netimobiledevice.Exceptions;
using Netimobiledevice.Plist;
using Netimobiledevice.Usbmuxd.Responses;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;

namespace Netimobiledevice.Usbmuxd
{
    internal class UsbmuxdConnectionMonitor
    {
        private readonly BackgroundWorker bw;
        private readonly Action<UsbmuxdDevice, UsbmuxdConnectionEventType> callback;
        private readonly Action<Exception>? errorCallback;
        /// <summary>
        /// The internal logger
        /// </summary>
        private readonly ILogger logger;

        private List<UsbmuxdDevice> Devices { get; set; } = new List<UsbmuxdDevice>();

        public UsbmuxdConnectionMonitor(ILogger logger, Action<UsbmuxdDevice, UsbmuxdConnectionEventType> callback, Action<Exception>? errorCallback = null)
        {
            this.logger = logger;

            bw = new BackgroundWorker {
                WorkerSupportsCancellation = true
            };
            bw.DoWork += BackgroundWorker_DoWork;

            this.callback = callback;
            this.errorCallback = errorCallback;
        }

        public UsbmuxdConnectionMonitor(Action<UsbmuxdDevice, UsbmuxdConnectionEventType> callback, Action<Exception>? errorCallback = null) : this(NullLogger<UsbmuxdConnectionMonitor>.Instance, callback, errorCallback) { }

        private void AddDevice(UsbmuxdDevice usbmuxdDevice)
        {
            Devices.Add(usbmuxdDevice);
            callback(usbmuxdDevice, UsbmuxdConnectionEventType.DEVICE_ADD);
        }

        private void BackgroundWorker_DoWork(object? sender, DoWorkEventArgs e)
        {
            do {
                UsbmuxConnection muxConnection;
                try {
                    muxConnection = UsbmuxConnection.Create();
                }
                catch (UsbmuxConnectionException ex) {
                    errorCallback?.Invoke(ex);
                    logger.LogWarning($"Issue trying to create UsbmuxConnection {ex.Message}");

                    // Put a delay here so that it doesn't immedietly retry creating the UsbmuxConnection
                    Thread.Sleep(500);
                    continue;
                }

                UsbmuxdResult usbmuxError = muxConnection.Listen();
                if (usbmuxError != UsbmuxdResult.Ok) {
                    continue;
                }

                while (!bw.CancellationPending) {
                    UsbmuxdResult result = GetNextEvent(muxConnection);
                    if (result != UsbmuxdResult.Ok) {
                        break;
                    }
                }
            } while (!bw.CancellationPending);
        }

        /// <summary>
        /// Waits for an event to occur, i.e. a packet coming from usbmuxd.
        /// Calls GenerateEvent to pass the event via callback to the client program.
        /// </summary>
        private UsbmuxdResult GetNextEvent(UsbmuxConnection connection)
        {
            // Block until we receive something 
            (UsbmuxdHeader header, byte[] payload) = connection.Receive();
            if (header.Length <= 0) {
                if (!bw.CancellationPending) {
                    logger.LogError($"Error in usbmuxd connection, disconnecting all devices!");
                }

                // When then usbmuxd connection fails, generate remove events for every device that
                // is still present so applications know about it
                foreach (UsbmuxdDevice device in Devices) {
                    Devices.Remove(device);
                    callback(device, UsbmuxdConnectionEventType.DEVICE_REMOVE);
                }
                return UsbmuxdResult.UnknownError;
            }

            if (header.Length > Marshal.SizeOf(header) && payload.Length == 0) {
                logger.LogError($"Invalid packet received, payload is missing");
                return UsbmuxdResult.UnknownError;
            }

            switch (header.Message) {
                case UsbmuxdMessageType.Add: {
                    AddResponse response = new AddResponse(header, payload);
                    UsbmuxdDevice usbmuxdDevice = new UsbmuxdDevice(response.DeviceRecord.DeviceId, response.DeviceRecord.SerialNumber, UsbmuxdConnectionType.Usb);
                    AddDevice(usbmuxdDevice);
                    break;
                }
                case UsbmuxdMessageType.Remove: {
                    RemoveResponse response = new RemoveResponse(header, payload);
                    RemoveDevice(response.DeviceId);
                    break;
                }
                case UsbmuxdMessageType.Paired: {
                    PairedResposne response = new PairedResposne(header, payload);
                    PairedDevice(response.DeviceId);
                    break;
                }
                case UsbmuxdMessageType.Plist: {
                    PlistResponse response = new PlistResponse(header, payload);
                    DictionaryNode responseDict = response.Plist.AsDictionaryNode();
                    string messageType = responseDict["MessageType"].AsStringNode().Value;
                    if (messageType == "Attached") {
                        UsbmuxdDevice usbmuxdDevice = new UsbmuxdDevice(responseDict["DeviceID"].AsIntegerNode(), responseDict["Properties"].AsDictionaryNode());
                        AddDevice(usbmuxdDevice);
                    }
                    else if (messageType == "Detached") {
                        ulong deviceId = responseDict["DeviceID"].AsIntegerNode().Value;
                        RemoveDevice(deviceId);
                    }
                    else if (messageType == "Paired") {
                        ulong deviceId = responseDict["DeviceID"].AsIntegerNode().Value;
                        PairedDevice(deviceId);
                    }
                    else {
                        throw new UsbmuxException($"Unexpected message type {header.Message} with length {header.Length}");
                    }
                    break;
                }
                default: {
                    if (header.Length > 0) {
                        logger.LogWarning($"Unexpected message type {header.Message} with length {header.Length}");
                    }
                    break;
                }
            }

            return UsbmuxdResult.Ok;
        }

        private void PairedDevice(ulong deviceId)
        {
            if (Devices.Exists(x => x.DeviceId == deviceId)) {
                UsbmuxdDevice? device = Devices.Find(x => x.DeviceId == deviceId);
                if (device != null) {
                    callback(device, UsbmuxdConnectionEventType.DEVICE_PAIRED);
                }
            }
            else {
                logger.LogWarning($"Got device paired message for id {deviceId}, but couldn't find the corresponding device in the list. This event will be ignored.");
            }
        }

        private void RemoveDevice(ulong deviceId)
        {
            if (Devices.Exists(x => x.DeviceId == deviceId)) {
                UsbmuxdDevice? device = Devices.Find(x => x.DeviceId == deviceId);
                if (device != null) {
                    Devices.Remove(device);
                    callback(device, UsbmuxdConnectionEventType.DEVICE_REMOVE);
                }
            }
            else {
                logger.LogWarning($"Got device remove message for id {deviceId}, but couldn't find the corresponding device in the list. This event will be ignored.");
            }
        }

        public void Start()
        {
            bw.RunWorkerAsync();
        }

        public void Stop()
        {
            bw.CancelAsync();
        }
    }
}

using Netimobiledevice.Exceptions;
using Netimobiledevice.Plist;
using Netimobiledevice.Usbmuxd.Responses;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Netimobiledevice.Usbmuxd
{
    internal class UsbmuxdConnectionMonitor
    {
        private readonly BackgroundWorker bw;
        private readonly Action<UsbmuxdDevice, UsbmuxdConnectionEventType> callback;

        private List<UsbmuxdDevice> Devices { get; set; } = new List<UsbmuxdDevice>();

        public UsbmuxdConnectionMonitor(Action<UsbmuxdDevice, UsbmuxdConnectionEventType> callback)
        {
            bw = new BackgroundWorker {
                WorkerSupportsCancellation = true
            };
            bw.DoWork += BackgroundWorker_DoWork;

            this.callback = callback;
        }

        private void AddDevice(UsbmuxdDevice usbmuxdDevice)
        {
            Devices.Add(usbmuxdDevice);
            callback(usbmuxdDevice, UsbmuxdConnectionEventType.DEVICE_ADD);
        }

        private void BackgroundWorker_DoWork(object? sender, DoWorkEventArgs e)
        {
            bool running = true;
            do {
                var muxConnection = UsbmuxConnection.Create();

                UsbmuxdResult usbmuxError = muxConnection.Listen();
                if (usbmuxError != UsbmuxdResult.Ok) {
                    continue;
                }

                while (running && !bw.CancellationPending) {
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
                    Debug.WriteLine($"Error in usbmuxd connection, disconnecting all devices!");
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
                Debug.WriteLine($"Invalid packet received, payload is missing");
                return UsbmuxdResult.UnknownError;
            }

            if (header.Message == UsbmuxdMessageType.Add) {
                AddResponse response = new AddResponse(header, payload);
                UsbmuxdDevice usbmuxdDevice = new UsbmuxdDevice(response.DeviceRecord.DeviceId, response.DeviceRecord.SerialNumber, UsbmuxdConnectionType.Usb);
                AddDevice(usbmuxdDevice);
            }
            else if (header.Message == UsbmuxdMessageType.Remove) {
                RemoveResponse response = new RemoveResponse(header, payload);
                RemoveDevice(response.DeviceId);
            }
            else if (header.Message == UsbmuxdMessageType.Paired) {
                PairedResposne response = new PairedResposne(header, payload);
                PairedDevice(response.DeviceId);
            }
            else if (header.Message == UsbmuxdMessageType.Plist) {
                PlistResponse response = new PlistResponse(header, payload);
                DictionaryNode responseDict = response.Plist.AsDictionaryNode();
                string messageType = responseDict["MessageType"].AsStringNode().Value;
                if (messageType == "Attached") {
                    UsbmuxdDevice usbmuxdDevice = new UsbmuxdDevice(responseDict["DeviceID"].AsIntegerNode(), responseDict["Properties"].AsDictionaryNode());
                    AddDevice(usbmuxdDevice);
                }
                else if (messageType == "Detached") {
                    long deviceId = responseDict["DeviceID"].AsIntegerNode().Value;
                    RemoveDevice(deviceId);
                }
                else if (messageType == "Paired") {
                    long deviceId = responseDict["DeviceID"].AsIntegerNode().Value;
                    PairedDevice(deviceId);
                }
                else {
                    throw new UsbmuxException($"Unexpected message type {header.Message} with length {header.Length}");
                }
            }
            else if (header.Length > 0) {
                Debug.WriteLine($"Unexpected message type {header.Message} with length {header.Length}");
            }

            return UsbmuxdResult.Ok;
        }

        private void PairedDevice(long deviceId)
        {
            if (Devices.Exists(x => x.DeviceId == deviceId)) {
                UsbmuxdDevice device = Devices.Find(x => x.DeviceId == deviceId);
                callback(device, UsbmuxdConnectionEventType.DEVICE_PAIRED);
            }
            else {
                Debug.WriteLine($"WARNING: got device paired message for id {deviceId}, but couldn't find the corresponding device in the list. This event will be ignored.");
            }
        }

        private void RemoveDevice(long deviceId)
        {
            if (Devices.Exists(x => x.DeviceId == deviceId)) {
                UsbmuxdDevice device = Devices.Find(x => x.DeviceId == deviceId);
                Devices.Remove(device);
                callback(device, UsbmuxdConnectionEventType.DEVICE_REMOVE);
            }
            else {
                Debug.WriteLine($"WARNING: got device remove message for id {deviceId}, but couldn't find the corresponding device in the list. This event will be ignored.");
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

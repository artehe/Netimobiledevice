using Microsoft.Extensions.Logging;
using Netimobiledevice.Plist;
using Netimobiledevice.Usbmuxd.Responses;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Usbmuxd;

internal class UsbmuxdConnectionMonitor(Action<UsbmuxdDevice, UsbmuxdConnectionEventType> callback, Action<Exception>? errorCallback = null, ILogger? logger = null)
{
    private readonly Action<UsbmuxdDevice, UsbmuxdConnectionEventType> _callback = callback;
    private readonly ConcurrentDictionary<long, UsbmuxdDevice> _connectedDevices = [];
    private readonly Action<Exception>? _errorCallback = errorCallback;
    /// <summary>
    /// The internal logger
    /// </summary>
    private readonly ILogger? _logger = logger;

    private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

    private Task? _connectionMonitorTask;

    private void AddDevice(UsbmuxdDevice usbmuxdDevice)
    {
        _connectedDevices.TryAdd(usbmuxdDevice.DeviceId, usbmuxdDevice);
        _callback(usbmuxdDevice, UsbmuxdConnectionEventType.Add);
    }

    private async Task ConnectionListener()
    {
        CancellationToken cancellationToken = _cancellationTokenSource.Token;
        do {
            UsbmuxConnection muxConnection;
            try {
                muxConnection = UsbmuxConnection.Create(logger: _logger);
            }
            catch (UsbmuxConnectionException ex) {
                _errorCallback?.Invoke(ex);
                _logger?.LogWarning(ex, "Issue trying to create UsbmuxConnection");

                // Put a delay here so that it doesn't immedietly retry creating the UsbmuxConnection
                await Task.Delay(500).ConfigureAwait(false);
                continue;
            }

            UsbmuxdResult usbmuxError = await muxConnection.ListenAsync(cancellationToken).ConfigureAwait(false);
            if (usbmuxError != UsbmuxdResult.Ok) {
                continue;
            }

            while (!cancellationToken.IsCancellationRequested) {
                UsbmuxdResult result = await GetAndProcessNextEvent(muxConnection, cancellationToken).ConfigureAwait(false);
                if (result != UsbmuxdResult.Ok) {
                    break;
                }
            }
        } while (!cancellationToken.IsCancellationRequested);
    }

    /// <summary>
    /// Waits for an event to occur, i.e. a packet coming from usbmuxd.
    /// Calls GenerateEvent to pass the event via callback to the client program.
    /// </summary>
    private async Task<UsbmuxdResult> GetAndProcessNextEvent(UsbmuxConnection connection, CancellationToken cancellationToken = default)
    {
        UsbmuxPacket packet = await connection.ReceiveAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        if (packet.Header.Length <= 0) {
            if (!cancellationToken.IsCancellationRequested) {
                _logger?.LogError("Error in usbmuxd connection, disconnecting all devices!");
            }

            // When then usbmuxd connection fails, generate remove events for every device that
            // is still present so applications know something has happened
            foreach (long deviceId in _connectedDevices.Keys) {
                _connectedDevices.Remove(deviceId, out UsbmuxdDevice? device);
                if (device is not null) {
                    _callback(device, UsbmuxdConnectionEventType.Remove);
                }
            }
            return UsbmuxdResult.UnknownError;
        }

        if (packet.Header.Length > Marshal.SizeOf(packet.Header) && packet.Header.Length == 0) {
            _logger?.LogError("Invalid packet received, payload is missing");
            return UsbmuxdResult.UnknownError;
        }

        switch (packet.Header.Message) {
            case UsbmuxdMessageType.Add: {
                AddResponse response = new AddResponse(packet.Header, packet.Payload);
                UsbmuxdDevice usbmuxdDevice = new UsbmuxdDevice(response.DeviceRecord.DeviceId, response.DeviceRecord.SerialNumber, UsbmuxdConnectionType.Usb);
                AddDevice(usbmuxdDevice);
                break;
            }
            case UsbmuxdMessageType.Remove: {
                RemoveResponse response = new RemoveResponse(packet.Header, packet.Payload);
                RemoveDevice(response.DeviceId);
                break;
            }
            case UsbmuxdMessageType.Paired: {
                PairedResposne response = new PairedResposne(packet.Header, packet.Payload);
                PairedDevice(response.DeviceId);
                break;
            }
            case UsbmuxdMessageType.Plist: {
                PlistResponse response = new PlistResponse(packet.Header, packet.Payload);
                DictionaryNode responseDict = response.Plist.AsDictionaryNode();
                string messageType = responseDict["MessageType"].AsStringNode().Value;
                if (messageType == "Attached") {
                    UsbmuxdDevice usbmuxdDevice = new UsbmuxdDevice(responseDict["DeviceID"].AsIntegerNode(), responseDict["Properties"].AsDictionaryNode());
                    AddDevice(usbmuxdDevice);
                }
                else if (messageType == "Detached") {
                    long deviceId = responseDict["DeviceID"].AsIntegerNode().SignedValue;
                    RemoveDevice(deviceId);
                }
                else if (messageType == "Paired") {
                    long deviceId = responseDict["DeviceID"].AsIntegerNode().SignedValue;
                    PairedDevice(deviceId);
                }
                else {
                    throw new UsbmuxException($"Unexpected message type {packet.Header.Message} with length {packet.Header.Length}");
                }
                break;
            }
            default: {
                if (packet.Header.Length > 0) {
                    _logger?.LogWarning("Unexpected message type {Message} with length {Length}", packet.Header.Message, packet.Header.Length);
                }
                break;
            }
        }

        return UsbmuxdResult.Ok;
    }

    private void PairedDevice(long deviceId)
    {
        if (_connectedDevices.TryGetValue(deviceId, out UsbmuxdDevice? device)) {
            _callback(device, UsbmuxdConnectionEventType.Paired);
        }
        else {
            _logger?.LogWarning("Got device paired message for id {deviceId}, but couldn't find the corresponding device in the list. This event will be ignored.", deviceId);
        }
    }

    private void RemoveDevice(long deviceId)
    {
        if (_connectedDevices.TryRemove(deviceId, out UsbmuxdDevice? device)) {
            _callback(device, UsbmuxdConnectionEventType.Remove);
        }
        else {
            _logger?.LogWarning("Got device remove message for id {deviceId}, but couldn't find the corresponding device in the list. This event will be ignored.", deviceId);
        }
    }

    public void Start()
    {
        if (_connectionMonitorTask == null) {
            _cancellationTokenSource = new CancellationTokenSource();
            _connectionMonitorTask = Task.Run(ConnectionListener, _cancellationTokenSource.Token);
        }
    }

    public void Stop()
    {
        _cancellationTokenSource.Cancel();
        _connectionMonitorTask = null;
    }
}

using Microsoft.Extensions.Logging;
using Netimobiledevice.Plist;
using Netimobiledevice.Usbmuxd.Responses;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Netimobiledevice.Usbmuxd;

public sealed class UsbmuxdConnectionMonitor(Action<Exception>? errorCallback = null, ILogger? logger = null) : IAsyncDisposable {
    private readonly ConcurrentDictionary<long, UsbmuxdDevice> _connectedDevices = [];
    private readonly Action<Exception>? _errorCallback = errorCallback;
    private readonly Channel<UsbmuxdConnectionEvent> _events = Channel.CreateUnbounded<UsbmuxdConnectionEvent>();
    private readonly Lock _lock = new();

    /// <summary>
    /// The internal logger
    /// </summary>
    private readonly ILogger? _logger = logger;

    private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

    private Task? _connectionMonitorTask;

    private async Task AddDevice(UsbmuxdDevice usbmuxdDevice, CancellationToken cancellationToken) {
        _connectedDevices.TryAdd(usbmuxdDevice.DeviceId, usbmuxdDevice);
        await _events.Writer.WriteAsync(new UsbmuxdConnectionEvent(usbmuxdDevice, UsbmuxdConnectionEventType.Add), cancellationToken);
    }

    private async Task ConnectionListener() {
        CancellationToken ct = _cancellationTokenSource.Token;
        do {
            UsbmuxConnection muxConnection;
            try {
                muxConnection = UsbmuxConnection.Create(logger: _logger);
            }
            catch (UsbmuxConnectionException ex) {
                _errorCallback?.Invoke(ex);
                _logger?.LogWarning(ex, "Issue trying to create UsbmuxConnection");

                // Put a delay here so that it doesn't immedietly retry creating the UsbmuxConnection
                await Task.Delay(500, ct).ConfigureAwait(false);
                continue;
            }

            using (muxConnection) {
                UsbmuxdResult usbmuxError = await muxConnection.ListenAsync(ct).ConfigureAwait(false);
                if (usbmuxError != UsbmuxdResult.Ok) {
                    continue;
                }

                while (!ct.IsCancellationRequested) {
                    UsbmuxdResult result = await GetAndProcessNextEvent(muxConnection, ct).ConfigureAwait(false);
                    if (result != UsbmuxdResult.Ok) {
                        break;
                    }
                }
            }
        } while (!ct.IsCancellationRequested);
    }

    /// <summary>
    /// Waits for an event to occur, i.e. a packet coming from usbmuxd.
    /// Calls GenerateEvent to pass the event via callback to the client program.
    /// </summary>
    private async Task<UsbmuxdResult> GetAndProcessNextEvent(UsbmuxConnection connection, CancellationToken cancellationToken) {
        UsbmuxPacket packet = await connection.ReceiveAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        if (packet.Header.Length <= 0) {
            if (!cancellationToken.IsCancellationRequested) {
                _logger?.LogError("Error in usbmuxd connection, disconnecting all devices!");
            }

            // When then usbmuxd connection fails, generate remove events for every device that
            // is still present so applications know something has happened
            await RemoveAllDevices(cancellationToken);
            return UsbmuxdResult.UnknownError;
        }

        if (packet.Header.Length > Marshal.SizeOf<UsbmuxdHeader>() && packet.Payload.Length == 0) {
            _logger?.LogError("Invalid packet received, payload is missing");
            return UsbmuxdResult.UnknownError;
        }

        switch (packet.Header.Message) {
            case UsbmuxdMessageType.Add: {
                AddResponse response = new AddResponse(packet.Header, packet.Payload);
                UsbmuxdDevice usbmuxdDevice = new UsbmuxdDevice(response.DeviceRecord.DeviceId, response.DeviceRecord.SerialNumber, UsbmuxdConnectionType.Usb);
                await AddDevice(usbmuxdDevice, cancellationToken);
                break;
            }
            case UsbmuxdMessageType.Remove: {
                RemoveResponse response = new RemoveResponse(packet.Header, packet.Payload);
                await RemoveDevice(response.DeviceId, cancellationToken);
                break;
            }
            case UsbmuxdMessageType.Paired: {
                PairedResposne response = new PairedResposne(packet.Header, packet.Payload);
                await PairedDevice(response.DeviceId, cancellationToken);
                break;
            }
            case UsbmuxdMessageType.Plist: {
                PlistResponse response = new PlistResponse(packet.Header, packet.Payload);
                DictionaryNode responseDict = response.Plist.AsDictionaryNode();
                string messageType = responseDict["MessageType"].AsStringNode().Value;
                switch (messageType) {
                    case "Attached": {
                        UsbmuxdDevice usbmuxdDevice = new UsbmuxdDevice(responseDict["DeviceID"].AsIntegerNode(), responseDict["Properties"].AsDictionaryNode());
                        await AddDevice(usbmuxdDevice, cancellationToken);
                        break;
                    }

                    case "Detached": {
                        long deviceId = responseDict["DeviceID"].AsIntegerNode().SignedValue;
                        await RemoveDevice(deviceId, cancellationToken);
                        break;
                    }

                    case "Paired": {
                        long deviceId = responseDict["DeviceID"].AsIntegerNode().SignedValue;
                        await PairedDevice(deviceId, cancellationToken);
                        break;
                    }

                    default: {
                        throw new UsbmuxException($"Unexpected message type {packet.Header.Message} with length {packet.Header.Length}");
                    }
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

    private async Task PairedDevice(long deviceId, CancellationToken cancellationToken) {
        if (_connectedDevices.TryGetValue(deviceId, out UsbmuxdDevice? device)) {
            await _events.Writer.WriteAsync(new UsbmuxdConnectionEvent(device, UsbmuxdConnectionEventType.Paired), cancellationToken);
        }
        else {
            _logger?.LogWarning("Got device paired message for id {deviceId}, but couldn't find the corresponding device in the list. This event will be ignored.", deviceId);
        }
    }

    private async Task RemoveAllDevices(CancellationToken cancellationToken) {
        foreach ((long deviceId, UsbmuxdDevice _) in _connectedDevices) {
            await RemoveDevice(deviceId, cancellationToken);
        }
    }

    private async Task RemoveDevice(long deviceId, CancellationToken cancellationToken) {
        if (_connectedDevices.TryRemove(deviceId, out UsbmuxdDevice? device)) {
            await _events.Writer.WriteAsync(new UsbmuxdConnectionEvent(device, UsbmuxdConnectionEventType.Remove), cancellationToken);
        }
        else {
            _logger?.LogWarning("Got device remove message for id {deviceId}, but couldn't find the corresponding device in the list. This event will be ignored.", deviceId);
        }
    }

    public async ValueTask DisposeAsync() {
        await StopAsync();
        _cancellationTokenSource.Dispose();
    }

    public void Start() {
        lock (_lock) {
            if (_connectionMonitorTask != null) {
                return;
            }

            _cancellationTokenSource = new CancellationTokenSource();
            _connectionMonitorTask = ConnectionListener();
        }
    }

    public void Stop() {
        _cancellationTokenSource.Cancel();
        _connectionMonitorTask = null;
    }

    public async Task StopAsync() {
        _cancellationTokenSource.Cancel();
        if (_connectionMonitorTask is { } task) {
            try {
                await task.ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
        }
        _connectionMonitorTask = null;
    }

    public IAsyncEnumerable<UsbmuxdConnectionEvent> WatchAsync(CancellationToken cancellationToken = default) {
        return _events.Reader.ReadAllAsync(cancellationToken);
    }
}

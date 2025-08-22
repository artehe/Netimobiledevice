using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;

namespace Netimobiledevice.Usbmuxd;

public static class Usbmux
{
    private static UsbmuxdConnectionMonitor? connectionMonitor;

    /// <summary>
    /// Get the device by UDID with given options and returns device information.
    /// </summary>
    /// <param name="udid">A device UDID of the device to look for.</param>
    /// <param name="connectionType">
    /// Specifying what device connection type should be considered during 
    /// lookup. If null will return any device found matching the udid prefering
    /// USB connections
    /// </param>
    /// <returns>The device info.</returns>
    public static UsbmuxdDevice? GetDevice(string udid, UsbmuxdConnectionType? connectionType = null, string usbmuxAddress = "")
    {
        UsbmuxdDevice? tmp = null;
        foreach (UsbmuxdDevice device in GetDeviceList(usbmuxAddress: usbmuxAddress)) {
            if (connectionType != null && device.ConnectionType != connectionType) {
                // If a specific connectionType was desired and not of this one then skip
                continue;
            }

            if (!string.IsNullOrEmpty(udid) && device.Serial != udid) {
                // If a specific udid was desired and not of this one then skip
                continue;
            }

            // Save the best result as a temporary
            tmp = device;

            if (device.ConnectionType == UsbmuxdConnectionType.Usb) {
                // Always prefer USB connection
                return device;
            }
        }
        return tmp;
    }

    /// <summary>
    /// Contacts usbmuxd and retrieves a list of connected devices.
    /// </summary>
    /// <returns>
    /// A list of connected Usbmux devices
    /// </returns>
    public static List<UsbmuxdDevice> GetDeviceList(string usbmuxAddress = "", ILogger? logger = null)
    {
        logger ??= NullLogger.Instance;
        UsbmuxConnection muxConnection = UsbmuxConnection.Create(usbmuxAddress, logger);
        muxConnection.UpdateDeviceList(100);
        List<UsbmuxdDevice> devices = muxConnection.Devices;
        muxConnection.Close();
        return devices;
    }

    public static bool IsDeviceConnected(string udid, UsbmuxdConnectionType? connectionType = null)
    {
        UsbmuxdDevice? device = GetDevice(udid, connectionType);
        return device != null;
    }

    /// <summary>
    /// Subscribes a callback function to be called upon device add/remove events from
    /// usbmux.
    /// </summary>
    /// <param name="callback">A callback function that is executed when an event occurs.</param>
    /// <param name="errorCallback">A callback function which is excecuted when an exception occurs</param>
    public static void Subscribe(Action<UsbmuxdDevice, UsbmuxdConnectionEventType> callback, Action<Exception>? errorCallback = null, ILogger? logger = null)
    {
        logger ??= NullLogger.Instance;
        connectionMonitor ??= new UsbmuxdConnectionMonitor(callback, errorCallback, logger);
        connectionMonitor.Start();
    }


    /// <summary>
    /// Stops monitoring for connection events from usbmuxd and removes the callback function
    /// </summary>
    public static void Unsubscribe()
    {
        connectionMonitor?.Stop();
        connectionMonitor = null;
    }
}

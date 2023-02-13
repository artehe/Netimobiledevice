using System;
using System.Collections.Generic;

namespace Netimobiledevice.Usbmuxd
{
    public static class Usbmux
    {
        private static UsbmuxdConnectionMonitor? connectionMonitor;

        /// <summary>
        /// Contacts usbmuxd and retrieves a list of connected devices.
        /// </summary>
        /// <returns>
        /// A list of connected Usbmux devices
        /// </returns>
        public static List<UsbmuxdDevice> GetDeviceList()
        {
            var muxConnection = UsbmuxConnection.Create();
            muxConnection.UpdateDeviceList(100);
            List<UsbmuxdDevice> devices = muxConnection.Devices;
            muxConnection.Close();
            return devices;
        }

        /// <summary>
        /// Subscribes a callback function to be called upon device add/remove events from
        /// usbmux.
        /// </summary>
        /// <param name="callback">A callback function that is executed when an event occurs.</param>
        public static void Subscribe(Action<UsbmuxdDevice, UsbmuxdConnectionEventType> callback)
        {
            connectionMonitor = new UsbmuxdConnectionMonitor(callback);
            connectionMonitor.Start();
        }

        /// <summary>
        /// Stops monitoring for connection events from usbmuxd and removes the callback function
        /// </summary>
        public static void Unsubscribe()
        {
            connectionMonitor?.Stop();
        }
    }
}

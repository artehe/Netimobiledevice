using System;

namespace Netimobiledevice.DeviceLink
{
    public class DLFileEventArgs : EventArgs
    {
        /// <summary>
        /// The file path related to the event.
        /// </summary>
        public string FilePath { get; }

        public DLFileEventArgs(string filePath)
        {
            FilePath = filePath;
        }
    }
}

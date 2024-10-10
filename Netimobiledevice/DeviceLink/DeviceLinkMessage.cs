namespace Netimobiledevice.DeviceLink
{
    /// <summary>
    /// DeviceLink Messages received from the device during the backup process.
    /// </summary>
    internal class DeviceLinkMessage
    {
        /// <summary>
        /// The device asks to receive files from the host.
        /// </summary>
        public const string DownloadFiles = "DLMessageDownloadFiles";
        /// <summary>
        /// The device asks to send files to the host.
        /// </summary>
        public const string UploadFiles = "DLMessageUploadFiles";
        /// <summary>
        /// The device asks for the free space on the host.
        /// </summary>
        public const string GetFreeDiskSpace = "DLMessageGetFreeDiskSpace";
        /// <summary>
        /// The device asks for the contents of an specific directory.
        /// </summary>
        public const string ContentsOfDirectory = "DLContentsOfDirectory";
        /// <summary>
        /// The device asks to create an specific directory on the host.
        /// </summary>
        public const string CreateDirectory = "DLMessageCreateDirectory";
        /// <summary>
        /// The device asks to move files on the host.
        /// </summary>
        public const string MoveFiles = "DLMessageMoveFiles";
        /// <summary>
        /// The device asks to move items on the host.
        /// </summary>
        public const string MoveItems = "DLMessageMoveItems";
        /// <summary>
        /// The device asks to remove files on the host.
        /// </summary>
        public const string RemoveFiles = "DLMessageRemoveFiles";
        /// <summary>
        /// The device asks to remove items on the host.
        /// </summary>
        public const string RemoveItems = "DLMessageRemoveItems";
        /// <summary>
        /// The device asks to copy items on the host.
        /// </summary>
        public const string CopyItem = "DLMessageCopyItem";
        /// <summary>
        /// The device asks the host to disconnect.
        /// </summary>
        public const string Disconnect = "DLMessageDisconnect";
        /// <summary>
        /// The device asks the host to process an error message.
        /// An error message with Code 0 is sent when the backup process is finished.
        /// </summary>
        public const string ProcessMessage = "DLMessageProcessMessage";
        /// <summary>
        /// The device tells the host how many disk space it requires so the host can try to make room.
        /// </summary>
        public const string PurgeDiskSpace = "DLMessagePurgeDiskSpace";
    }
}

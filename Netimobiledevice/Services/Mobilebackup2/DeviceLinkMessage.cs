namespace Netimobiledevice.Services.Mobilebackup2;

/// <summary>
/// DeviceLink Messages received from the device during the backup process.
/// </summary>
internal sealed class DeviceLinkMessage {
    /// <summary>
    /// The device asks for the contents of an specific directory.
    /// </summary>
    public const string ContentsOfDirectory = "DLContentsOfDirectory";
    /// <summary>
    /// The device asks to copy items on the host.
    /// </summary>
    public const string CopyItem = "DLMessageCopyItem";
    /// <summary>
    /// The device asks to create an specific directory on the host.
    /// </summary>
    public const string CreateDirectory = "DLMessageCreateDirectory";
    /// <summary>
    /// The device asks the host to disconnect.
    /// </summary>
    public const string Disconnect = "DLMessageDisconnect";
    /// <summary>
    /// The device asks to receive files from the host.
    /// </summary>
    public const string DownloadFiles = "DLMessageDownloadFiles";
    public const string FileErrorCode = "DLFileErrorCode";
    public const string FileErrorString = "DLFileErrorString";
    public const string FileModificationDate = "DLFileModificationDate";
    public const string FileSize = "DLFileSize";
    public const string FileType = "DLFileType";
    public const string FileTypeDirectory = "DLFileTypeDirectory";
    public const string FileTypeRegular = "DLFileTypeRegular";
    public const string FileTypeUnknown = "DLFileTypeUnknown";
    /// <summary>
    /// The device asks for the free space on the host.
    /// </summary>
    public const string GetFreeDiskSpace = "DLMessageGetFreeDiskSpace";
    public const string MessageStatusResponse = "DLMessageStatusResponse";
    /// <summary>
    /// The device asks to move files on the host.
    /// </summary>
    public const string MoveFiles = "DLMessageMoveFiles";
    /// <summary>
    /// The device asks to move items on the host.
    /// </summary>
    public const string MoveItems = "DLMessageMoveItems";
    /// <summary>
    /// The device asks the host to process an error message.
    /// An error message with Code 0 is sent when the backup process is finished.
    /// </summary>
    public const string ProcessMessage = "DLMessageProcessMessage";
    /// <summary>
    /// The device tells the host how many disk space it requires so the host can try to make room.
    /// </summary>
    /// <remarks>
    /// iOS 26 sends DLMessagePurgeDiskSpace as a routine pre-backup space check, not as a signal that the host is actually out of space.
    /// </remarks>
    public const string PurgeDiskSpace = "DLMessagePurgeDiskSpace";
    /// <summary>
    /// The device asks to remove files on the host.
    /// </summary>
    public const string RemoveFiles = "DLMessageRemoveFiles";
    /// <summary>
    /// The device asks to remove items on the host.
    /// </summary>
    public const string RemoveItems = "DLMessageRemoveItems";
    public const string Ready = "DLMessageDeviceReady";
    /// <summary>
    /// The device asks to send files to the host.
    /// </summary>
    public const string UploadFiles = "DLMessageUploadFiles";
    public const string VersionExchange = "DLMessageVersionExchange";
    public const string VersionsOk = "DLVersionsOk";
}

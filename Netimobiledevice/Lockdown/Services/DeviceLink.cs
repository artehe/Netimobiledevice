using Netimobiledevice.EndianBitConversion;
using Netimobiledevice.Plist;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netimobiledevice.Lockdown.Services
{
    internal class DeviceLink : IDisposable
    {
        private const int FILE_DATA_CODE = 0x0C;
        private const int SUCCESS_CODE = 0x00;

        private readonly ServiceConnection _service;
        private readonly string? _rootPath;
        private readonly Dictionary<string, Func<ArrayNode, Task>> _deviceLinkMessageHandlers = new Dictionary<string, Func<ArrayNode, Task>>();

        public DeviceLink(ServiceConnection service, string? rootPath)
        {
            _service = service;
            _rootPath = rootPath;

            _deviceLinkMessageHandlers.Add("DLMessageDownloadFiles", DownloadFiles);
            _deviceLinkMessageHandlers.Add("DLMessageGetFreeDiskSpace", GetFreeDiskSpace);
            _deviceLinkMessageHandlers.Add("DLMessageCreateDirectory", CreateDirectory);
            _deviceLinkMessageHandlers.Add("DLMessageUploadFiles", UploadFiles);
            _deviceLinkMessageHandlers.Add("DLMessageMoveItems", MoveItems);
            _deviceLinkMessageHandlers.Add("DLMessageRemoveItems", RemoveItems);
            _deviceLinkMessageHandlers.Add("DLContentsOfDirectory", ContentsOfDirectory);
            _deviceLinkMessageHandlers.Add("DLMessageCopyItem", CopyItem);
        }

        private async Task ContentsOfDirectory(ArrayNode message)
        {
            string targetPath = Path.Combine(_rootPath ?? string.Empty, message[1].AsStringNode().Value);
            DictionaryNode dirList = new DictionaryNode();

            var dir = new DirectoryInfo(targetPath);
            if (dir.Exists) {
                foreach (FileSystemInfo entry in dir.GetFileSystemInfos()) {
                    var entryDict = new DictionaryNode {
                        { "DLFileModificationDate", new DateNode(entry.LastWriteTime) },
                        { "DLFileSize", new IntegerNode(entry is FileInfo fileInfo ? fileInfo.Length : 0L) },
                        { "DLFileType", new StringNode(entry.Attributes.HasFlag(FileAttributes.Directory) ? "DLFileTypeDirectory" : "DLFileTypeRegular") }
                    };
                    dirList.Add(entry.Name, entryDict);
                }
            }

            StatusResponse(0, extraStatus: dirList);
        }

        private async Task CopyItem(ArrayNode message)
        {
            string sourcePath = Path.Combine(_rootPath ?? string.Empty, message[1].AsStringNode().Value);
            string destinationPath = Path.Combine(_rootPath ?? string.Empty, message[2].AsStringNode().Value);

            var source = new FileInfo(sourcePath);
            if (source.Attributes.HasFlag(FileAttributes.Directory)) {
                Debug.WriteLine($"Are you really asking me to copy a whole directory?");
            }
            else {
                File.Copy(source.FullName, new FileInfo(destinationPath).FullName);
            }

            StatusResponse(0);
        }

        private async Task CreateDirectory(ArrayNode message)
        {
            string path = message[1].AsStringNode().Value;
            if (!string.IsNullOrWhiteSpace(_rootPath)) {
                path = Path.Combine(_rootPath, path);
            }
            Directory.CreateDirectory(path);
            StatusResponse(0);
        }

        private void Disconnect()
        {
            ArrayNode message = new ArrayNode {
                new StringNode("DLMessageDisconnect"),
                new StringNode("___EmptyParameterString___")
            };
            _service.SendPlist(message);
        }

        private async Task DownloadFiles(ArrayNode message)
        {
            DictionaryNode status = new DictionaryNode();
            foreach (StringNode file in message[1].AsArrayNode().Cast<StringNode>()) {
                byte[] filePathBytes = Encoding.UTF8.GetBytes(file.Value);
                byte[] pathLen = EndianBitConverter.BigEndian.GetBytes(filePathBytes.Length);
                _service.Send(pathLen);
                _service.Send(filePathBytes);

                string filePath = Path.Combine(_rootPath ?? string.Empty, file.Value);

                var fileInfo = new FileInfo(filePath);
                if (!fileInfo.Exists) {
                    string errorMessage = "No such file or directory";
                    int errCode = -6;

                    status.Add(file.Value, new DictionaryNode() {
                        { "DLFileErrorString", new StringNode(errorMessage) },
                        { "DLFileErrorCode", new IntegerNode(errCode) }
                    });

                    List<byte> buffer = new List<byte>();
                    byte[] errBytes = Encoding.UTF8.GetBytes(errorMessage);
                    buffer.AddRange(EndianBitConverter.BigEndian.GetBytes(errBytes.Length + 1));
                    buffer.Add((byte) errCode);
                    buffer.AddRange(errBytes);

                    _service.Send(buffer.ToArray());
                }
                else {
                    byte[] data = File.ReadAllBytes(filePath);

                    _service.Send(EndianBitConverter.BigEndian.GetBytes(data.Length + 1));
                    _service.Send(EndianBitConverter.BigEndian.GetBytes(FILE_DATA_CODE));

                    _service.Send(data);

                    // Send success code
                    byte[] buffer = new byte[] { 0, 0, 0, 1, 0 };
                    _service.Send(buffer);
                }
            }

            // Send the file transfer terminator
            _service.Send(new byte[] { 0x00, 0x00, 0x00, 0x00 });
            if (status.Count > 0) {
                StatusResponse(-13, "Multi status", status);
            }
            else {
                StatusResponse(0);
            }
        }

        private async Task GetFreeDiskSpace(ArrayNode message)
        {
            long freeSpace = 0;

            string rootPath = Path.GetPathRoot(_rootPath) ?? string.Empty;
            DriveInfo[] allDrives = DriveInfo.GetDrives();
            foreach (DriveInfo d in allDrives) {
                if (d.Name.Contains(rootPath)) {
                    freeSpace = d.AvailableFreeSpace;
                    break;
                }
            }

            StatusResponse(0, null, new IntegerNode(freeSpace));
        }

        private async Task MoveItems(ArrayNode message)
        {
            foreach (KeyValuePair<string, PropertyNode> move in message[1].AsDictionaryNode()) {
                string newPath = move.Value.AsStringNode().Value;
                if (!string.IsNullOrEmpty(newPath)) {
                    var newFile = new FileInfo(Path.Combine(_rootPath ?? string.Empty, newPath));
                    var oldFile = new FileInfo(Path.Combine(_rootPath ?? string.Empty, move.Key));
                    var fileInfo = new FileInfo(newPath);
                    if (fileInfo.Exists) {
                        if (fileInfo.Attributes.HasFlag(FileAttributes.Directory)) {
                            new DirectoryInfo(newFile.FullName).Delete(true);
                        }
                        else {
                            fileInfo.Delete();
                        }
                    }

                    if (oldFile.Exists) {
                        oldFile.MoveTo(newFile.FullName);
                    }
                }
            }

            StatusResponse(0);
        }

        private async Task<byte[]> PrefixedReceiveAsync()
        {
            byte[] data = await _service.ReceiveAsync(4);
            int size = EndianBitConverter.BigEndian.ToInt32(data, 0);
            return _service.Receive(size);
        }

        private async Task RemoveItems(ArrayNode message)
        {
            ArrayNode items = message[1].AsArrayNode();
            foreach (StringNode filename in items.Cast<StringNode>()) {
                if (string.IsNullOrEmpty(filename.Value)) {
                    Debug.WriteLine("Empty file to remove.");
                }
                else {
                    FileInfo file = new FileInfo(Path.Combine(_rootPath ?? string.Empty, filename.Value));
                    if (file.Exists) {
                        if (file.Attributes.HasFlag(FileAttributes.Directory)) {
                            Directory.Delete(file.FullName, true);
                        }
                        else {
                            file.Delete();
                        }
                    }
                }
            }
        }

        private void StatusResponse(int statusCode, string? statusString = null, PropertyNode? extraStatus = null)
        {
            ArrayNode responseMessage = new ArrayNode {
                new StringNode("DLMessageStatusResponse"),
                new IntegerNode(statusCode)
            };

            if (string.IsNullOrWhiteSpace(statusString)) {
                responseMessage.Add(new StringNode("___EmptyParameterString___"));
            }
            else {
                responseMessage.Add(new StringNode(statusString));
            }

            if (extraStatus == null) {
                responseMessage.Add(new DictionaryNode());
            }
            else {
                responseMessage.Add(extraStatus);
            }

            _service.SendPlist(responseMessage);
        }

        private async Task UploadFiles(ArrayNode message)
        {
            do {
                byte[] deviceFilenameBytes = await PrefixedReceiveAsync();
                string deviceFilename = Encoding.UTF8.GetString(deviceFilenameBytes);
                if (string.IsNullOrWhiteSpace(deviceFilename)) {
                    break;
                }

                byte[] filenameBytes = await PrefixedReceiveAsync();
                string filename = Encoding.UTF8.GetString(filenameBytes);

                byte[] sizeBytes = await _service.ReceiveAsync(4);
                int size = EndianBitConverter.BigEndian.ToInt32(sizeBytes, 0);
                byte code = (await _service.ReceiveAsync(1))[0];

                string filePath = Path.Combine(_rootPath ?? string.Empty, filename);
                using (FileStream fs = new FileStream(filePath, FileMode.Create)) {
                    int blockSize = size;
                    int done = 0;

                    while (size > 0 && code == FILE_DATA_CODE) {
                        while (done < blockSize) {
                            int toRead = Math.Min(blockSize - done, 4096);
                            byte[] buffer = await _service.ReceiveAsync(toRead);

                            fs.Write(buffer);
                            done += buffer.Length;
                        }

                        sizeBytes = await _service.ReceiveAsync(4);
                        size = EndianBitConverter.BigEndian.ToInt32(sizeBytes, 0);
                        code = (await _service.ReceiveAsync(1))[0];
                    }
                }

                if (code != SUCCESS_CODE) {
                    throw new Exception($"Issue receiving files from device error code: {code}");
                }
            } while (true);

            StatusResponse(0);
        }


        public void Dispose()
        {
            Disconnect();
        }

        public async Task<PropertyNode> MessageLoop(Action<PropertyNode>? progressCallback)
        {
            while (true) {
                ArrayNode message = await ReceiveMessage();
                StringNode command = message[0].AsStringNode();

                string[] progressThreeCommands = new string[] {
                    "DLMessageDownloadFiles",
                    "DLMessageMoveFiles",
                    "DLMessageMoveItems",
                    "DLMessageRemoveFiles"
                };

                if (progressThreeCommands.Contains(command.Value)) {
                    progressCallback?.Invoke(message[3]);
                }
                else if (command.Value == "DLMessageProcessMessage") {
                    if (!message[1].AsDictionaryNode().ContainsKey("ErrorCode")) {
                        return message[1].AsDictionaryNode()["Content"];
                    }
                    else {
                        throw new Exception($"Device link error: \n {PropertyList.SaveAsString(message, PlistFormat.Xml)}");
                    }
                }

                await _deviceLinkMessageHandlers[command.Value](message);
            }
        }

        public async Task<ArrayNode> ReceiveMessage()
        {
            PropertyNode? message = await _service.ReceivePlist();
            if (message == null) {
                return new ArrayNode();
            }
            return message.AsArrayNode();
        }

        public void SendProcessMessage(PropertyNode message)
        {
            _service.SendPlist(new ArrayNode() {
                new StringNode("DLMessageProcessMessage"),
                message
            });
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

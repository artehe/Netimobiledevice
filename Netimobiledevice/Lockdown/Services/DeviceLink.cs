using Netimobiledevice.EndianBitConversion;
using Netimobiledevice.Plist;
using System;
using System.Collections.Generic;
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
        private readonly Dictionary<string, Action<ArrayNode>> _deviceLinkMessageHandlers = new Dictionary<string, Action<ArrayNode>>();

        public DeviceLink(ServiceConnection service, string? rootPath)
        {
            _service = service;
            _rootPath = rootPath;

            _deviceLinkMessageHandlers.Add("DLMessageDownloadFiles", DownloadFiles);
            _deviceLinkMessageHandlers.Add("DLMessageGetFreeDiskSpace", GetFreeDiskSpace);
            _deviceLinkMessageHandlers.Add("DLMessageCreateDirectory", CreateDirectory);
            _deviceLinkMessageHandlers.Add("DLMessageUploadFiles", UploadFiles);
            // TODO deviceLinkMessageHandlers.Add("DLMessageMoveItems", self.move_items);
            // TODO deviceLinkMessageHandlers.Add("DLMessageRemoveItems", self.remove_items);
            // TODO deviceLinkMessageHandlers.Add("DLContentsOfDirectory", self.contents_of_directory);
            // TODO deviceLinkMessageHandlers.Add("DLMessageCopyItem", self.copy_item);
        }

        private void CreateDirectory(ArrayNode message)
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

        private void DownloadFiles(ArrayNode message)
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
                    buffer.AddRange(EndianBitConversion.EndianBitConverter.BigEndian.GetBytes(errBytes.Length + 1));
                    buffer.Add((byte) errCode);
                    buffer.AddRange(errBytes);

                    _service.Send(buffer.ToArray());
                }
                else {
                    byte[] data = File.ReadAllBytes(filePath);

                    _service.Send(EndianBitConversion.EndianBitConverter.BigEndian.GetBytes(data.Length + 1));
                    _service.Send(EndianBitConversion.EndianBitConverter.BigEndian.GetBytes(FILE_DATA_CODE));

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

        private void GetFreeDiskSpace(ArrayNode message)
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

        private byte[] PrefixedReceive()
        {
            byte[] data = _service.Receive(4);
            int size = EndianBitConverter.BigEndian.ToInt32(data, 0);
            return _service.Receive(size);
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

        private void UploadFiles(ArrayNode message)
        {
            while (true) {
                byte[] deviceNameBytes = PrefixedReceive();
                string deviceName = Encoding.UTF8.GetString(deviceNameBytes);
                if (string.IsNullOrWhiteSpace(deviceName)) {
                    break;
                }

                byte[] filenameBytes = PrefixedReceive();
                string filename = Encoding.UTF8.GetString(filenameBytes);

                int size = EndianBitConverter.BigEndian.ToInt32(_service.Receive(4), 0);
                byte code = _service.Receive(1)[0];
                size--;

                string filePath = Path.Combine(_rootPath ?? string.Empty, filename);

                using (FileStream fs = new FileStream(filePath, FileMode.Create)) {
                    while (size > 0 && code == FILE_DATA_CODE) {
                        fs.Write(_service.Receive(size));

                        size = EndianBitConverter.BigEndian.ToInt32(_service.Receive(4), 0);
                        code = _service.Receive(1)[0];
                        size--;
                    }
                }

                if (code != SUCCESS_CODE) {
                    throw new Exception($"Issue receiving files from device {code}");
                }
            }

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

                _deviceLinkMessageHandlers[command.Value](message);
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

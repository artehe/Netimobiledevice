using Netimobiledevice.Extentions;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Lockdown.Services;
using Netimobiledevice.Plist;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Netimobiledevice.Afc
{
    public sealed class AfcService : BaseService
    {
        private const int MAXIMUM_READ_SIZE = 1024 ^ 2; // 1 MB

        private static readonly Dictionary<string, AfcFileOpenMode> FileOpenModes = new Dictionary<string, AfcFileOpenMode>() {
            { "r", AfcFileOpenMode.ReadOnly },
            { "r+", AfcFileOpenMode.ReadWrite },
            {"w", AfcFileOpenMode.WriteOnly },
            {"w+", AfcFileOpenMode.WriteReadTruncate },
            { "a", AfcFileOpenMode.Append},
            { "a+", AfcFileOpenMode.ReadAppend},
        };

        private ulong packetNumber = 0;

        protected override string ServiceName => "com.apple.afc";

        public AfcService(LockdownClient client) : base(client) { }

        private void DispatchPacket(AfcOpCode opCode, byte[] data, ulong? thisLength = null)
        {
            AfcHeader header = new AfcHeader() {
                EntireLength = (ulong) data.Length,
                Length = (ulong) data.Length,
                PacketNumber = packetNumber,
                Operation = opCode
            };
            header.EntireLength = (ulong) (header.GetBytes().Length + data.Length);
            header.Length = (ulong) (header.GetBytes().Length + data.Length);

            if (thisLength != null) {
                header.Length = (ulong) thisLength;
            }

            packetNumber++;

            List<byte> packet = new List<byte>();
            packet.AddRange(header.GetBytes());
            packet.AddRange(data);

            Service.Send(packet.ToArray());
        }

        private byte[] FileRead(ulong handle, ulong size)
        {
            List<byte> data = new List<byte>();
            while (size > 0) {
                ulong toRead;
                if (size > MAXIMUM_READ_SIZE) {
                    toRead = MAXIMUM_READ_SIZE;
                }
                else {
                    toRead = size;
                }

                AfcFileReadRequest readRequest = new AfcFileReadRequest() {
                    Handle = handle,
                    Size = size
                };

                DispatchPacket(AfcOpCode.Read, readRequest.GetBytes());
                (AfcError status, byte[] chunk) = ReceiveData();
                if (status != AfcError.Success) {
                    throw new AfcException(status, "File Read Error");
                }

                size -= toRead;
                data.AddRange(chunk);
            }

            return data.ToArray();
        }

        private DictionaryNode GetFileInfo(string filename)
        {
            Dictionary<string, string> stat;
            try {
                AfcFileInfoRequest request = new AfcFileInfoRequest(new CString(filename, Encoding.UTF8));
                byte[] response = RunOperation(AfcOpCode.GetFileInfo, request.GetBytes());
                stat = ParseFileInfoResponseToDict(response);
            }
            catch (AfcException ex) {
                if (ex.AfcError != AfcError.ReadError) {
                    throw;
                }
                throw new AfcFileNotFoundException(ex.AfcError, filename);
            }

            // Convert timestamps from unix epoch ticks (nanoseconds) to DateTime
            long divisor = (long) Math.Pow(10, 6);
            long mTimeMilliseconds = long.Parse(stat["st_mtime"]) / divisor;
            long birthTimeMilliseconds = long.Parse(stat["st_birthtime"]) / divisor;

            DateTime mTime = DateTimeOffset.FromUnixTimeMilliseconds(mTimeMilliseconds).LocalDateTime;
            DateTime birthTime = DateTimeOffset.FromUnixTimeMilliseconds(birthTimeMilliseconds).LocalDateTime;

            DictionaryNode fileInfo = new DictionaryNode {
                { "st_ifmt", new StringNode(stat["st_ifmt"]) },
                { "st_size", new IntegerNode(ulong.Parse(stat["st_size"])) },
                { "st_blocks", new IntegerNode(ulong.Parse(stat["st_blocks"])) },
                { "st_nlink", new IntegerNode(ulong.Parse(stat["st_nlink"])) },
                { "st_mtime", new DateNode(mTime) },
                { "st_birthtime", new DateNode(birthTime) }
            };

            return fileInfo;
        }

        private static Dictionary<string, string> ParseFileInfoResponseToDict(byte[] data)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();

            string decodedData = Encoding.UTF8.GetString(data);
            List<string> seperatedData = decodedData.Split('\0').ToList();

            seperatedData.RemoveAt(seperatedData.Count - 1);
            if (seperatedData.Count % 2 != 0) {
                throw new Exception("Received data not balanced, unable to parse to dictionary");
            }

            for (int i = 0; i < seperatedData.Count; i += 2) {
                result[seperatedData[i]] = seperatedData[i + 1];
            }
            return result;
        }

        private byte[] RunOperation(AfcOpCode opCode, byte[] data)
        {
            DispatchPacket(opCode, data);
            (AfcError status, byte[] recievedData) = ReceiveData();

            if (status != AfcError.Success) {
                if (status == AfcError.ObjectNotFound) {
                    throw new AfcFileNotFoundException(AfcError.ObjectNotFound);
                }
                throw new Exception($"Afc opcode {opCode} failed with status: {status}");
            }

            return recievedData;
        }

        private (AfcError, byte[]) ReceiveData()
        {
            byte[] response = Service.Receive(AfcHeader.GetSize());

            AfcError status = AfcError.Success;
            byte[] data = Array.Empty<byte>();

            if (response.Length > 0) {
                AfcHeader header = AfcHeader.FromBytes(response);
                if (header.EntireLength < (ulong) AfcHeader.GetSize()) {
                    throw new Exception("Expected more bytes in afc header than receieved");
                }
                int length = (int) header.EntireLength - AfcHeader.GetSize();
                data = Service.Receive(length);
                if (header.Operation == AfcOpCode.Status) {
                    if (length != 8) {
                        Debug.WriteLine("Status length is not 8 bytes long");
                    }
                    ulong statusValue = BitConverter.ToUInt64(data, 0);
                    status = (AfcError) statusValue;
                }
            }

            return (status, data);
        }

        private string ResolvePath(string filename)
        {
            DictionaryNode info = GetFileInfo(filename);
            if (info.ContainsKey("st_ifmt") && info["st_ifmt"].AsStringNode().Value == "S_IFLNK") {
                string target = info["LinkTarget"].AsStringNode().Value;
                if (!target.StartsWith("/")) {
                    // Relative path
                    filename = Path.Combine(Path.GetDirectoryName(filename), target);
                }
                else {
                    filename = target;
                }
            }
            return filename;
        }

        public byte[]? GetFileContents(string filename)
        {
            filename = ResolvePath(filename);

            DictionaryNode info = GetFileInfo(filename);
            if (info["st_ifmt"].AsStringNode().Value != "S_IFREG") {
                throw new AfcException(AfcError.InvalidArg, $"{filename} isn't a file");
            }

            ulong handle = FileOpen(filename);
            if (handle == 0) {
                return null;
            }
            byte[] details = FileRead(handle, info["st_size"].AsIntegerNode().Value);

            FileClose(handle);
            return details;
        }

        public byte[] Lock(ulong handle, AfcLockModes operation)
        {
            AfcLockRequest request = new AfcLockRequest() {
                Handle = handle,
                Op = (ulong) operation
            };
            return RunOperation(AfcOpCode.FileLock, request.GetBytes());
        }

        public byte[] FileClose(ulong handle)
        {
            AfcFileCloseRequest request = new AfcFileCloseRequest() {
                Handle = handle,
            };
            return RunOperation(AfcOpCode.FileClose, request.GetBytes());
        }

        public ulong FileOpen(string filename, string mode = "r")
        {
            if (!FileOpenModes.ContainsKey(mode)) {
                throw new ArgumentException($"mode can oly be one of {FileOpenModes.Keys}", nameof(mode));
            }

            AfcFileOpenRequest openRequest = new AfcFileOpenRequest(FileOpenModes[mode], new CString(filename, Encoding.UTF8));
            byte[] data = RunOperation(AfcOpCode.FileOpen, openRequest.GetBytes());
            return StructExtentions.FromBytes<AfcFileOpenResponse>(data).Handle;
        }
    }
}

using Microsoft.Extensions.Logging;
using Netimobiledevice.Extentions;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Plist;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Netimobiledevice.Afc
{
    public sealed class AfcService : LockdownService
    {
        private const string LOCKDOWN_SERVICE_NAME = "com.apple.afc";
        private const string RSD_SERVICE_NAME = "com.apple.afc.shim.remote";

        private const int MAXIMUM_READ_SIZE = 1024 ^ 2; // 1 MB

        private static readonly Dictionary<string, AfcFileOpenMode> FileOpenModes = new Dictionary<string, AfcFileOpenMode>() {
            { "r", AfcFileOpenMode.ReadOnly },
            { "r+", AfcFileOpenMode.ReadWrite },
            { "w", AfcFileOpenMode.WriteOnly },
            { "w+", AfcFileOpenMode.WriteReadTruncate },
            { "a", AfcFileOpenMode.Append},
            { "a+", AfcFileOpenMode.ReadAppend},
        };

        private ulong _packetNumber;

        public AfcService(LockdownServiceProvider lockdown, string serviceName, ILogger? logger = null) : base(lockdown, GetServiceName(lockdown, serviceName), logger: logger)
        {
            _packetNumber = 0;
        }

        public AfcService(LockdownServiceProvider client) : this(client, string.Empty) { }

        private static string GetServiceName(LockdownServiceProvider lockdown, string providedServiceName)
        {
            string serviceName = providedServiceName;
            if (string.IsNullOrEmpty(serviceName)) {
                if (lockdown is LockdownClient) {
                    serviceName = LOCKDOWN_SERVICE_NAME;
                }
                else {
                    serviceName = RSD_SERVICE_NAME;
                }
            }
            return serviceName;
        }

        private void DispatchPacket(AfcOpCode opCode, byte[] data, ulong? thisLength = null)
        {
            AfcHeader header = new AfcHeader() {
                EntireLength = (ulong) data.Length,
                Length = (ulong) data.Length,
                PacketNumber = _packetNumber,
                Operation = opCode
            };
            header.EntireLength = (ulong) (header.GetBytes().Length + data.Length);
            header.Length = (ulong) (header.GetBytes().Length + data.Length);

            if (thisLength != null) {
                header.Length = (ulong) thisLength;
            }

            _packetNumber++;

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

        public DictionaryNode GetFileInfo(string filename)
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
                throw new AfcException("Received data not balanced, unable to parse to dictionary");
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
                throw new AfcException($"Afc opcode {opCode} failed with status: {status}");
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
                        Logger?.LogWarning("Status length is not 8 bytes long");
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
                if (!target.StartsWith("/", StringComparison.InvariantCulture)) {
                    // Relative path
                    string filePath = Path.GetDirectoryName(filename) ?? string.Empty;
                    filename = Path.Combine(filePath, target);
                }
                else {
                    filename = target;
                }
            }
            return filename;
        }

        /// <summary>
        /// return true if succeess or raise an exception depending on force parameter.
        /// </summary>
        /// <param name="filename">path to directory or a file</param>
        /// <param name="force">True for ignore exception and return False</param>
        /// <returns></returns>
        private bool RmSingle(string filename, bool force = false)
        {
            try {
                RunOperation(AfcOpCode.RemovePath, new AfcRmRequest(new CString(filename, Encoding.UTF8)).GetBytes());
                return true;
            }
            catch (AfcException) {
                if (force) {
                    return false;
                }
                else {
                    throw;
                }
            }
        }

        public bool Exists(string filename)
        {
            try {
                GetFileInfo(filename);
                return true;
            }
            catch (AfcException ex) {
                if (ex.AfcError == AfcError.ObjectNotFound) {
                    return false;
                }
                else {
                    throw;
                }
            }
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
            if (!FileOpenModes.TryGetValue(mode, out AfcFileOpenMode value)) {
                throw new ArgumentException($"mode can oly be one of {FileOpenModes.Keys}", nameof(mode));
            }
            AfcFileOpenRequest openRequest = new AfcFileOpenRequest(value, new CString(filename, Encoding.UTF8));
            byte[] data = RunOperation(AfcOpCode.FileOpen, openRequest.GetBytes());
            return StructExtentions.FromBytes<AfcFileOpenResponse>(data).Handle;
        }

        public List<string> GetDirectoryList()
        {
            List<string> directoryList = new List<string>();
            try {
                AfcFileInfoRequest request = new AfcFileInfoRequest(new CString("/", Encoding.UTF8));
                byte[] response = RunOperation(AfcOpCode.ReadDir, request.GetBytes());
                directoryList = ParseFileInfoResponseForMessage(response);
            }
            catch (Exception ex) {
                Logger?.LogError(ex, "Error trying to get directory list");
            }
            return directoryList;
        }

        private List<string> ListDirectory(string filename)
        {
            byte[] data = RunOperation(AfcOpCode.ReadDir, new AfcReadDirectoryRequest(filename).GetBytes());
            // Make sure to skip "." and ".."
            return AfcReadDirectoryResponse.Parse(data).Filenames.Skip(2).ToList();
        }

        private IEnumerable<Tuple<string, List<string>, List<string>>> Walk(string directory)
        {
            List<string> directories = new List<string>();
            List<string> files = new List<string>();

            foreach (string fd in ListDirectory(directory)) {
                if (new string[] { ".", "..", "" }.Contains(fd)) {
                    continue;
                }

                DictionaryNode fileInfo = GetFileInfo($"{directory}/{fd}");
                if (fileInfo != null && fileInfo.TryGetValue("st_ifmt", out PropertyNode? value)) {
                    if (value is StringNode node && node.Value == "S_IFDIR") {
                        directories.Add(fd);
                    }
                    else {
                        files.Add(fd);
                    }
                }
            }
            yield return Tuple.Create(directory, directories, files);

            foreach (string dir in directories) {
                foreach (Tuple<string, List<string>, List<string>> result in Walk($"{directory}/{dir}")) {
                    yield return result;
                }
            }
        }

        public bool IsDir(string filename)
        {
            DictionaryNode stat = GetFileInfo(filename);
            if (stat.TryGetValue("st_ifmt", out PropertyNode? value)) {
                return value.AsStringNode().Value == "S_IFDIR";
            }
            return false;
        }

        /// <summary>
        /// List the files and folders in the given directory
        /// </summary>
        /// <param name="path">Path to list</param>
        /// <param name="depth">Listing depth, -1 to list infinite depth</param>
        /// <returns>List of files found</returns>
        public IEnumerable<string> LsDirectory(string path, int depth = -1)
        {
            foreach ((string folder, List<string> dirs, List<string> files) in Walk(path)) {
                if (folder == path) {
                    yield return folder;
                    if (depth == 0) {
                        break;
                    }
                }
                if (folder != path && depth != -1 && folder.Count(x => x == Path.DirectorySeparatorChar) >= depth) {
                    continue;
                }

                List<string> results = new List<string>();
                results.AddRange(dirs.ToArray());
                results.AddRange(files.ToArray());
                foreach (string entry in results) {
                    yield return $"{folder}/{entry}";
                }
            }
        }

        public void Pull(string relativeSrc, string dst, string srcDir = "")
        {
            string src = srcDir;
            if (string.IsNullOrEmpty(src)) {
                src = relativeSrc;
            }
            else {
                src = $"{src}/{relativeSrc}";
            }

            Logger?.LogInformation("{src} --> {dst}", src, dst);

            src = ResolvePath(src);

            if (!IsDir(src)) {
                // Normal file
                if (Path.EndsInDirectorySeparator(dst)) {
                    string[] splitSrc = relativeSrc.Split('/');
                    string filename = splitSrc[splitSrc.Length - 1];
                    dst = Path.Combine(dst, filename);
                }
                using (FileStream fs = new FileStream(dst, FileMode.Create)) {
                    fs.Write(GetFileContents(src));
                }
            }
            else {
                // Directory
                string dstPath = $"{dst}/{Path.GetDirectoryName(relativeSrc)}";
                Directory.CreateDirectory(dstPath);

                foreach (string filename in ListDirectory(src)) {
                    string srcFilename = $"{src}/{filename}";
                    string dstFilename = Path.Combine(dstPath, filename);

                    srcFilename = ResolvePath(srcFilename);

                    if (IsDir(srcFilename)) {
                        Directory.CreateDirectory(dstFilename);
                    }
                    Pull(srcFilename, dstPath);
                }
            }
        }

        /// <summary>
        /// Recursive removal of a directory or a file, if any did not succeed then return list of undeleted filenames or raise exception depending on force parameter.
        /// </summary>
        /// <param name="filename">path to directory or a file</param>
        /// <param name="force">True for ignore exception and return list of undeleted paths</param>
        /// <returns>A list of undeleted paths</returns>
        public List<string> Rm(string filename, bool force = false)
        {
            if (!Exists(filename)) {
                if (!RmSingle(filename, force: force)) {
                    return new List<string>() { filename };
                }
            }

            // Single file
            if (!IsDir(filename)) {
                if (RmSingle(filename, force: force)) {
                    return new List<string>();
                }
                return new List<string>() { filename };
            }

            // Directory Content
            List<string> undeletedItems = new List<string>();
            foreach (string entry in ListDirectory(filename)) {
                string currentFile = $"{filename}/{entry}";
                if (IsDir(currentFile)) {
                    List<string> retUndeletedItems = Rm(currentFile, force: true);
                    undeletedItems.AddRange(retUndeletedItems);
                }
                else {
                    if (!RmSingle(currentFile, force: true)) {
                        undeletedItems.Add(currentFile);
                    }
                }

            }

            // Directory Path
            try {
                if (!RmSingle(filename, force: force)) {
                    undeletedItems.Add(filename);
                    return undeletedItems;
                }
            }
            catch (AfcException) {
                if (undeletedItems.Count > 0) {
                    undeletedItems.Add(filename);
                }
                else {
                    throw;
                }
            }

            if (undeletedItems.Count > 0) {
                throw new AfcException($"Failed to delete paths: {string.Join(", ", undeletedItems)}");
            }

            return new List<string>();
        }

        private static List<string> ParseFileInfoResponseForMessage(byte[] data)
        {
            string decodedData = Encoding.UTF8.GetString(data);
            List<string> seperatedData = decodedData.Split('\0').ToList();
            seperatedData.RemoveAt(seperatedData.Count - 1);
            return seperatedData;
        }
    }
}

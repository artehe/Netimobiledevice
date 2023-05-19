using Netimobiledevice.Extentions;
using Netimobiledevice.Usbmuxd;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Netimobiledevice.Lockdown.Services
{
    internal enum AfcFileOpenMode : ulong
    {
        ReadOnly = 0x00000001, // r O_RDONLY
        ReadWrite = 0x00000002, // r+  O_RDWR | O_CREAT
        WriteOnly = 0x00000003, // w   O_WRONLY | O_CREAT  | O_TRUNC
        WriteReadTruncate = 0x00000004, // w+  O_RDWR | O_CREAT | O_TRUNC
        Append = 0x00000005,  // O_WRONLY | O_APPEND | O_CREAT
        ReadAppend = 0x00000006 // a+ O_RDWR | O_APPEND | O_CREAT
    }

    internal enum AfcOpCode : ulong
    {
        Status = 0x00000001,
        Data = 0x00000002, // Data
        ReadDir = 0x00000003, // ReadDir
        ReadFile = 0x00000004, // ReadFile
        WriteFile = 0x00000005, // WriteFile
        WritePart = 0x00000006, // WritePart
        Truncate = 0x00000007, // TruncateFile
        RemovePath = 0x00000008, // RemovePath
        MakeDir = 0x00000009, // MakeDir
        GetFileInfo = 0x0000000a, // GetFileInfo
        GetDeviceInfo = 0x0000000b, // GetDeviceInfo
        WriteFileAtomic = 0x0000000c, // WriteFileAtomic (tmp file+rename)
        FileOpen = 0x0000000d, // FileRefOpen
        FileOpenResult = 0x0000000e, // FileRefOpenResult
        Read = 0x0000000f, // FileRefRead 
        Write = 0x00000010, // FileRefWrite
        FileSeek = 0x00000011, // FileRefSeek 
        FileTell = 0x00000012, // FileRefTell 
        FileTellResult = 0x00000013, // FileRefTellResult 
        FileClose = 0x00000014, // FileRefClose 
        FileSetSize = 0x00000015, // FileRefSetFileSize (ftruncate) 
        GetConInfo = 0x00000016, // GetConnectionInfo 
        SetConOptions = 0x00000017, // SetConnectionOptions 
        RenamePath = 0x00000018, // RenamePath 
        SetFsBs = 0x00000019, // SetFSBlockSize (0x800000) 
        SetSocketBs = 0x0000001A, // SetSocketBlockSize (0x800000) 
        FileLock = 0x0000001B, // FileRefLock 
        MakeLink = 0x0000001C, // MakeLink
        SetFileTime = 0x0000001E, // set st_mtime
    }

    public enum AfcError : ulong
    {
        Success = 0,
        UnknownError = 1,
        OpHeaderInvalid = 2,
        NoResources = 3,
        ReadError = 4,
        WriteError = 5,
        UnknownPacketType = 6,
        InvalidArg = 7,
        ObjectNotFound = 8,
        ObjectIsDir = 9,
        PermDenied = 10,
        ServiceNotConnected = 11,
        OpTimeout = 12,
        TooMuchData = 13,
        EndOfData = 14,
        OpNotSupported = 15,
        ObjectExists = 16,
        ObjectBusy = 17,
        NoSpaceLeft = 18,
        OpWouldBlock = 19,
        IoError = 20,
        OpInterrupted = 21,
        OpInProgress = 22,
        InternalError = 23,
        MuxError = 30,
        NoMem = 31,
        NotEnoughData = 32,
        DirNotEmpty = 33,
    }

    public enum AfcLockModes : ulong
    {
        SharedLock = 1 | 4,
        ExclusiveLock = 2 | 4,
        Unlock = 8 | 4
    }

    internal struct AfcLockRequest
    {
        public ulong Handle;
        public ulong Op;
    }

    internal struct AfcFileOpenRequest
    {
        public AfcFileOpenMode Mode;
        public string Filename;
    }

    internal struct AfcFileCloseRequest
    {
        public ulong Handle;
    }

    internal struct AfcFileOpenResponse
    {
        public ulong Handle;
    }

    internal struct AfcHeader
    {
        public byte[] Magic;
        public ulong EntireLength;
        public ulong Length;
        public ulong PacketNumber;
        public AfcOpCode Operation;
    }

    public sealed class AfcService : BaseService
    {
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
                Magic = new byte[] { 0x43, 0x46, 0x41, 0x36, 0x4C, 0x50, 0x41, 0x41 },
                EntireLength = (ulong) (Marshal.SizeOf(typeof(UsbmuxdHeader)) + data.Length),
                Length = (ulong) (Marshal.SizeOf(typeof(UsbmuxdHeader)) + data.Length),
                PacketNumber = packetNumber,
                Operation = opCode
            };

            if (thisLength != null) {
                header.Length = (ulong) thisLength;
            }

            packetNumber++;

            List<byte> packet = new List<byte>();
            packet.AddRange(header.GetBytes());
            packet.AddRange(data);

            Service.Send(packet.ToArray());
        }

        private byte[] RunOperation(AfcOpCode opCode, byte[] data)
        {
            DispatchPacket(opCode, data);
            (AfcError status, byte[] recievedData) = ReceiveData();

            if (status != AfcError.Success) {
                throw new Exception($"Afc opcode {opCode} failed with status: {status}");
            }

            return recievedData;
        }

        private (AfcError, byte[]) ReceiveData()
        {
            byte[] response = Service.Receive(Marshal.SizeOf(typeof(UsbmuxdHeader)));

            AfcError status = AfcError.Success;
            byte[] data = Array.Empty<byte>();

            if (response.Length > 0) {
                AfcHeader header = StructExtentions.FromBytes<AfcHeader>(response);
                if (header.EntireLength <= (ulong) Marshal.SizeOf(typeof(UsbmuxdHeader))) {
                    throw new Exception("Expected more bytes in afc header than receieved");
                }
                int length = (int) header.EntireLength - Marshal.SizeOf(typeof(UsbmuxdHeader));
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

            AfcFileOpenRequest openRequest = new AfcFileOpenRequest() {
                Mode = FileOpenModes[mode],
                Filename = filename
            };
            byte[] data = RunOperation(AfcOpCode.FileOpen, openRequest.GetBytes());
            return StructExtentions.FromBytes<AfcFileOpenResponse>(data).Handle;
        }
    }
}

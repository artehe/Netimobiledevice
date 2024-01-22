using Netimobiledevice.EndianBitConversion;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Lockdown.Services;
using Netimobiledevice.Plist;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Diagnostics
{
    /// <summary>
    /// Provides the service to show process lists, stream formatted and/or filtered syslogs
    /// as well as getting old stored syslog archives in the PAX format.
    /// </summary>
    public sealed class OsTraceService : BaseService
    {
        protected override string ServiceName => "com.apple.os_trace_relay";

        public OsTraceService(LockdownClient client) : base(client) { }

        private SyslogEntry ParseSyslogData(List<byte> data)
        {
            data.RemoveRange(0, 9); // Skip the first 9 bytes            
            int pid = EndianBitConverter.LittleEndian.ToInt32(data.ToArray(), 0);
            data.RemoveRange(0, sizeof(int) + 42); // Skip size of int + 42 bytes
            DateTime timestamp = OsTraceService.ParseTimeStamp(data.Take(12));
            data.RemoveRange(0, 12 + 1); // Remove the size of the timestamp + 1 byte
            SyslogLevel level = (SyslogLevel) data[0];
            data.RemoveRange(0, 1 + 38); // Remove the enum byte followed by the next 38 bytes
            short imageNameSize = EndianBitConverter.LittleEndian.ToInt16(data.ToArray(), 0);
            short messageSize = EndianBitConverter.LittleEndian.ToInt16(data.ToArray(), 2);
            data.RemoveRange(0, sizeof(short) + sizeof(short) + 6); // Skip size of the two shorts + 6 bytes
            int subsystemSize = EndianBitConverter.LittleEndian.ToInt32(data.ToArray(), 0);
            int categorySize = EndianBitConverter.LittleEndian.ToInt32(data.ToArray(), 4);
            data.RemoveRange(0, sizeof(int) + sizeof(int) + 6); // Skip size of the two ints + 4 bytes

            int filenameSize = 0;
            for (int i = 0; i < data.Count; i++) {
                if (data[i] == 0x00) {
                    filenameSize = i + 1;
                    break;
                }
            }
            string filename = Encoding.UTF8.GetString(data.Take(filenameSize - 1).ToArray());
            data.RemoveRange(0, filenameSize); // Remove the filename bytes

            string imageName = Encoding.UTF8.GetString(data.Take(imageNameSize - 1).ToArray());
            data.RemoveRange(0, imageNameSize);

            string message = Encoding.UTF8.GetString(data.Take(messageSize - 1).ToArray());
            data.RemoveRange(0, messageSize);

            SyslogLabel? label = null;
            if (data.Count > 0) {
                string subsystem = Encoding.UTF8.GetString(data.Take(subsystemSize - 1).ToArray());
                data.RemoveRange(0, subsystemSize);
                string category = Encoding.UTF8.GetString(data.Take(categorySize - 1).ToArray());
                data.RemoveRange(0, categorySize);
                label = new SyslogLabel(category, subsystem);
            }

            return new SyslogEntry(pid, timestamp, level, imageName, filename, message, label);
        }

        private static DateTime ParseTimeStamp(IEnumerable<byte> data)
        {
            int seconds = EndianBitConverter.LittleEndian.ToInt32(data.ToArray(), 0);
            int microseconds = EndianBitConverter.LittleEndian.ToInt32(data.ToArray(), 8) / 1000000;
            return DateTime.UnixEpoch.AddSeconds(seconds).AddMilliseconds(microseconds * 1000);
        }

        public async Task<DictionaryNode> GetPidList(CancellationToken cancellationToken = default)
        {
            DictionaryNode request = new DictionaryNode() {
                { "Request", new StringNode("PidList") },
            };
            await Service.SendPlistAsync(request, cancellationToken);

            // Ignore the first received unknown byte
            await Service.ReceiveAsync(1, cancellationToken);

            DictionaryNode response = (await Service.ReceivePlistAsync(cancellationToken))?.AsDictionaryNode() ?? new DictionaryNode();
            return response;
        }

        public void CreateArchive(string outputPath, int? sizeLimit = null, int? ageLimit = null, int? startTime = null)
        {
            var request = new DictionaryNode() {
                { "Request", new StringNode("CreateArchive") }
            };

            if (sizeLimit != null) {
                request.Add("SizeLimit", new IntegerNode((int) sizeLimit));
            }
            if (ageLimit != null) {
                request.Add("AgeLimit", new IntegerNode((int) ageLimit));
            }
            if (startTime != null) {
                request.Add("StartTime", new IntegerNode((int) startTime));
            }

            Service.SendPlist(request);

            int value = Service.Receive(1)[0];
            if (value != 1) {
                throw new Exception($"Invalid response got {value} instead of 1");
            }

            DictionaryNode plistResponse = Service.ReceivePlist()?.AsDictionaryNode() ?? new DictionaryNode();
            if (!plistResponse.TryGetValue("Status", out PropertyNode? status) || status.AsStringNode().Value != "RequestSuccessful") {
                throw new Exception($"Invalid status: {PropertyList.SaveAsString(plistResponse, PlistFormat.Xml)}");
            }

            using (FileStream f = new FileStream(outputPath, FileMode.Create, FileAccess.Write)) {
                while (true) {
                    try {
                        value = Service.Receive(1)[0];
                        if (value != 3) {
                            throw new Exception("Invalid magic");
                        }
                    }
                    catch (Exception) {
                        break;
                    }

                    byte[] data = Service.ReceivePrefixed();
                    f.Write(data);
                }
            }
        }

        public IEnumerable<SyslogEntry> WatchSyslog(int pid = -1)
        {
            DictionaryNode request = new DictionaryNode() {
                { "Request", new StringNode("StartActivity") },
                { "MessageFilter", new IntegerNode(65535) },
                { "Pid", new IntegerNode(pid) },
                { "StreamFlags", new IntegerNode(60) }
            };
            Service.SendPlist(request);

            byte[] lengthSizeBytes = Service.Receive(4);
            int lengthSize = EndianBitConverter.LittleEndian.ToInt32(lengthSizeBytes, 0);

            byte[] lengthBytes = Service.Receive(lengthSize);
            if (lengthBytes.Length < 4) {
                byte[] tmpArr = new byte[4];
                lengthBytes.CopyTo(tmpArr, 0);
                lengthBytes = tmpArr;
            }
            int length = EndianBitConverter.LittleEndian.ToInt32(lengthBytes, 0);

            byte[] responseBytes = Service.Receive(length);
            DictionaryNode response = PropertyList.LoadFromByteArray(responseBytes).AsDictionaryNode();

            if (!response.ContainsKey("Status") || response["Status"].AsStringNode().Value != "RequestSuccessful") {
                throw new Exception($"Received an invalid response: {response}");
            }

            while (true) {
                byte checkValue = Service.Receive(1)[0];
                if (checkValue != 0x02) {
                    throw new Exception($"Entry started with incorrect byte value: {checkValue}");
                }

                lengthBytes = Service.Receive(4);
                length = EndianBitConverter.LittleEndian.ToInt32(lengthBytes, 0);

                byte[] lineBytes = Service.Receive(length);
                yield return ParseSyslogData(lineBytes.ToList());
            }
        }
    }
}

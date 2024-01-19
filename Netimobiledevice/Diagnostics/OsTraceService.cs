using Netimobiledevice.EndianBitConversion;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Lockdown.Services;
using Netimobiledevice.Plist;
using System;
using System.Collections.Generic;
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

        private SyslogEntry ParseSyslogData(byte[] data)
        {
            return new SyslogEntry(0, DateTime.Now, SyslogLevel.Fault, string.Empty, string.Empty, string.Empty, null);
        }

        public async Task<DictionaryNode> GetPidList()
        {
            DictionaryNode request = new DictionaryNode() {
                { "Request", new StringNode("PidList") },
            };
            await Service.SendPlistAsync(request);

            // Ignore the first received unknown byte
            await Service.ReceiveAsync(1);

            DictionaryNode response = (await Service.ReceivePlistAsync())?.AsDictionaryNode() ?? new DictionaryNode();
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
            if (!plistResponse.TryGetValue("Status", out PropertyNode status) || status.AsStringNode().Value != "RequestSuccessful") {
                throw new Exception($"Invalid status: {status.AsStringNode().Value}");
            }

            using (FileStream f = new FileStream(outputPath, FileMode.Create, FileAccess.Write)) {
                using (StreamWriter s = new StreamWriter(f)) {
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
                        s.Write(data);
                    }
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
                yield return ParseSyslogData(lineBytes);
            }
        }
    }
}

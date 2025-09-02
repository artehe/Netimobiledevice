﻿using Microsoft.Extensions.Logging;
using Netimobiledevice.EndianBitConversion;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Plist;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Diagnostics;

/// <summary>
/// Provides the service to show process lists, stream formatted and/or filtered syslogs
/// as well as getting old stored syslog archives in the PAX format.
/// </summary>
public sealed class OsTraceService : LockdownService
{

    private const string LOCKDOWN_SERVICE_NAME = "com.apple.os_trace_relay";
    private const string RSD_SERVICE_NAME = "com.apple.os_trace_relay.shim.remote";

    public OsTraceService(LockdownServiceProvider lockdown, ILogger? logger = null) : base(lockdown, RSD_SERVICE_NAME, logger: logger) { }

    public OsTraceService(LockdownClient lockdown, ILogger? logger = null) : base(lockdown, LOCKDOWN_SERVICE_NAME, logger: logger) { }

    private static SyslogEntry ParseSyslogData(List<byte> data)
    {
        data.RemoveRange(0, 9); // Skip the first 9 bytes            
        int pid = EndianBitConverter.LittleEndian.ToInt32([.. data], 0);
        data.RemoveRange(0, sizeof(int) + 42); // Skip size of int + 42 bytes
        DateTime timestamp = ParseTimeStamp(data.Take(12));
        data.RemoveRange(0, 12 + 1); // Remove the size of the timestamp + 1 byte
        SyslogLevel level = (SyslogLevel) data[0];
        data.RemoveRange(0, 1 + 38); // Remove the enum byte followed by the next 38 bytes
        short imageNameSize = EndianBitConverter.LittleEndian.ToInt16([.. data], 0);
        short messageSize = EndianBitConverter.LittleEndian.ToInt16([.. data], 2);
        data.RemoveRange(0, sizeof(short) + sizeof(short) + 6); // Skip size of the two shorts + 6 bytes
        int subsystemSize = EndianBitConverter.LittleEndian.ToInt32([.. data], 0);
        int categorySize = EndianBitConverter.LittleEndian.ToInt32([.. data], 4);
        data.RemoveRange(0, sizeof(int) + sizeof(int) + 6); // Skip size of the two ints + 4 bytes

        int filenameSize = 0;
        for (int i = 0; i < data.Count; i++) {
            if (data[i] == 0x00) {
                filenameSize = i + 1;
                break;
            }
        }
        string filename = Encoding.UTF8.GetString([.. data.Take(filenameSize - 1)]);
        data.RemoveRange(0, filenameSize); // Remove the filename bytes

        string imageName = Encoding.UTF8.GetString([.. data.Take(imageNameSize - 1)]);
        data.RemoveRange(0, imageNameSize);

        string message = Encoding.UTF8.GetString([.. data.Take(messageSize - 1)]);
        data.RemoveRange(0, messageSize);

        SyslogLabel? label = null;
        if (data.Count > 0) {
            string subsystem = Encoding.UTF8.GetString([.. data.Take(subsystemSize - 1)]);
            data.RemoveRange(0, subsystemSize);
            string category = Encoding.UTF8.GetString([.. data.Take(categorySize - 1)]);
            data.RemoveRange(0, categorySize);
            label = new SyslogLabel(category, subsystem);
        }

        return new SyslogEntry(pid, timestamp, level, imageName, filename, message, label);
    }

    private static DateTime ParseTimeStamp(IEnumerable<byte> data)
    {
        int seconds = EndianBitConverter.LittleEndian.ToInt32([.. data], 0);
        int microseconds = EndianBitConverter.LittleEndian.ToInt32([.. data], 8) / 1000000;
        return DateTime.UnixEpoch.AddSeconds(seconds).AddMilliseconds(microseconds * 1000);
    }

    public DictionaryNode GetPidList()
    {
        DictionaryNode request = new DictionaryNode() {
            { "Request", new StringNode("PidList") },
        };
        Service.SendPlist(request);

        // Ignore the first received unknown byte
        Service.Receive(1);

        DictionaryNode response = Service.ReceivePlist()?.AsDictionaryNode() ?? [];
        return response;
    }

    public async Task<DictionaryNode> GetPidListAsync(CancellationToken cancellationToken = default)
    {
        DictionaryNode request = new DictionaryNode() {
            { "Request", new StringNode("PidList") },
        };
        await Service.SendPlistAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);

        // Ignore the first received unknown byte
        await Service.ReceiveAsync(1, cancellationToken).ConfigureAwait(false);

        PropertyNode? response = await Service.ReceivePlistAsync(cancellationToken).ConfigureAwait(false);
        DictionaryNode dict = response?.AsDictionaryNode() ?? [];
        return dict;
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
            throw new NetimobiledeviceException($"Invalid response got {value} instead of 1");
        }

        DictionaryNode plistResponse = Service.ReceivePlist()?.AsDictionaryNode() ?? [];
        if (!plistResponse.TryGetValue("Status", out PropertyNode? status) || status.AsStringNode().Value != "RequestSuccessful") {
            throw new NetimobiledeviceException($"Invalid status: {PropertyList.SaveAsString(plistResponse, PlistFormat.Xml)}");
        }

        using (FileStream f = new FileStream(outputPath, FileMode.Create, FileAccess.Write)) {
            while (true) {
                try {
                    value = Service.Receive(1)[0];
                    if (value != 3) {
                        throw new NetimobiledeviceException("Invalid magic");
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
            throw new NetimobiledeviceException($"Received an invalid response: {response}");
        }

        while (true) {
            byte checkValue = Service.Receive(1)[0];
            if (checkValue != 0x02) {
                throw new NetimobiledeviceException($"Entry started with incorrect byte value: {checkValue}");
            }

            lengthBytes = Service.Receive(4);
            length = EndianBitConverter.LittleEndian.ToInt32(lengthBytes, 0);

            byte[] lineBytes = Service.Receive(length);
            yield return ParseSyslogData([.. lineBytes]);
        }
    }

    public async IAsyncEnumerable<SyslogEntry> WatchSyslog(int pid = -1, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        DictionaryNode request = new DictionaryNode() {
            { "Request", new StringNode("StartActivity") },
            { "MessageFilter", new IntegerNode(65535) },
            { "Pid", new IntegerNode(pid) },
            { "StreamFlags", new IntegerNode(60) }
        };
        await Service.SendPlistAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);

        byte[] lengthSizeBytes = Service.Receive(4);
        int lengthSize = EndianBitConverter.LittleEndian.ToInt32(lengthSizeBytes, 0);

        byte[] lengthBytes = await Service.ReceiveAsync(lengthSize, cancellationToken).ConfigureAwait(false);
        if (lengthBytes.Length < 4) {
            byte[] tmpArr = new byte[4];
            lengthBytes.CopyTo(tmpArr, 0);
            lengthBytes = tmpArr;
        }
        int length = EndianBitConverter.LittleEndian.ToInt32(lengthBytes, 0);

        byte[] responseBytes = await Service.ReceiveAsync(length, cancellationToken).ConfigureAwait(false);
        DictionaryNode response = PropertyList.LoadFromByteArray(responseBytes).AsDictionaryNode();

        if (!response.ContainsKey("Status") || response["Status"].AsStringNode().Value != "RequestSuccessful") {
            throw new NetimobiledeviceException($"Received an invalid response: {response}");
        }

        while (!cancellationToken.IsCancellationRequested) {
            byte[] checkValue = await Service.ReceiveAsync(1, cancellationToken).ConfigureAwait(false);
            if (checkValue[0] != 0x02) {
                throw new NetimobiledeviceException($"Entry started with incorrect byte value: {checkValue}");
            }

            lengthBytes = await Service.ReceiveAsync(4, cancellationToken).ConfigureAwait(false);
            length = EndianBitConverter.LittleEndian.ToInt32(lengthBytes, 0);

            byte[] lineBytes = await Service.ReceiveAsync(length, cancellationToken).ConfigureAwait(false);
            yield return ParseSyslogData([.. lineBytes]);
        }
    }
}

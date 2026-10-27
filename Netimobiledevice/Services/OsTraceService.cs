using Microsoft.Extensions.Logging;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Plist;
using Netimobiledevice.Services.OsTrace;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Services;

/// <summary>
/// Client for the device's <c>com.apple.os_trace_relay</c> service.
/// <list type="bullet">
/// <item>List the running processes (pid to process name mapping).</item>
/// <item>Stream live syslog entries in binary form, optionally filtered by pid.</item>
/// <item>Retrieve the device's stored log archive (the contents of <c>/var/db/diagnostics</c>), either as a raw
/// PAX-format tar stream or extracted into a <c>.logarchive</c> directory consumable by <c>log</c> / Console.</item>
/// </list>
/// </summary>
public sealed class OsTraceService(
    LockdownServiceProvider lockdown,
    ILogger? logger = null
) : LockdownService(
    lockdown,
    lockdown is LockdownClient ? LockdownServiceName : RemoteServiceName,
    logger: logger
) {
    private const int MessageFilterAll = 0xFFFF;

    public const OsActivityStreamFlag DefaultStreamFlags =
        OsActivityStreamFlag.Payload |
        OsActivityStreamFlag.Historical |
        OsActivityStreamFlag.Callstack |
        OsActivityStreamFlag.Debug;

    public const string LockdownServiceName = "com.apple.os_trace_relay";
    public const string RemoteServiceName = "com.apple.os_trace_relay.shim.remote";

    /// <summary>
    /// UTF-8 decode with replacement characters
    /// </summary>
    private static string Decode(ReadOnlySpan<byte> data, int start, int length) {
        if (length <= 0 || start >= data.Length) {
            return string.Empty;
        }
        length = Math.Min(length, data.Length - start);
        return Encoding.UTF8.GetString(data.Slice(start, length));
    }

    /// <summary>
    /// Decode one binary syslog record from the os_trace_relay activity stream into a <see cref="SyslogEntry"/>.
    /// Fixed-offset fields (pid, timestamps, level, UUIDs, thread/proc ids) are unpacked from the record header,
    /// followed by the variable-length filename, image name, message, and optional subsystem/category label.
    /// The label is only populated when both subsystem and category are present in the record.
    /// </summary>
    /// <param name="data">The raw bytes of a single record (the payload following the stream's length prefix).</param>
    private static SyslogEntry ParseSyslogEntry(ReadOnlySpan<byte> data) {
        int offset = 0;

        // Skip first 9 bytes
        offset += 9;

        // pid (u32 LE)
        uint pid = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset, 4));
        offset += 4;

        // The next 42 bytes hold: procid at +0 (u64), then the process' main executable UUID at +8
        ulong procId = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(offset, 8));
        Guid processImageUuid = new Guid(data.Slice(offset + 8, 16), bigEndian: true);
        offset += 42;

        // Timestamp: seconds (u32), 4 skipped bytes, microseconds (u32)
        uint seconds = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset, 4));
        offset += 4;
        offset += 4;
        uint microseconds = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset, 4));
        offset += 4;
        DateTime timestamp = DateTimeOffset.FromUnixTimeSeconds(seconds)
            .AddTicks(microseconds * 10L)
            .LocalDateTime;

        // Skip 1 byte
        offset += 1;

        // level (1 byte)
        byte rawLevel = data[offset];
        offset += 1;

        // The next 38 bytes hold the raw mach timestamp at +4, the thread id at +14, and the sender image UUID at +22
        ulong machTimestamp = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(offset + 4, 8));
        ulong threadId = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(offset + 14, 8));
        Guid imageUuid = new Guid(data.Slice(offset + 22, 16), bigEndian: true);
        offset += 38;

        int imageNameSize = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(offset, 2));
        offset += 2;

        int messageSize = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(offset, 2));
        offset += 2;

        // Skip 2 bytes
        offset += 2;

        uint senderImageOffset = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset, 4));
        offset += 4;

        int subsystemSize = checked((int) BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset, 4)));
        offset += 4;

        int categorySize = checked((int) BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset, 4)));
        offset += 4;

        // Skip 4 bytes
        offset += 4;

        // Filename (null-terminated)
        int nul = data[offset..].IndexOf((byte) 0);
        if (nul < 0) {
            throw new OsTraceException("syslog record is missing the filename terminator");
        }
        string filename = Decode(data, offset, nul);
        offset += nul + 1;

        // Image name (size includes trailing NUL)
        string imageName = Decode(data, offset, imageNameSize - 1);
        offset += imageNameSize;

        // Message (size includes trailing NUL)
        string message = Decode(data, offset, messageSize - 1);
        offset += messageSize;

        // Label (optional)
        SyslogLabel? label = null;
        if (subsystemSize > 0 && categorySize > 0) {
            string subsystem = Decode(data, offset, subsystemSize - 1);
            offset += subsystemSize;
            string category = Decode(data, offset, categorySize - 1);
            label = new SyslogLabel(Category: category, Subsystem: subsystem);
        }

        SyslogLevel level = (SyslogLevel) rawLevel;
        if (!Enum.IsDefined(level)) {
            throw new OsTraceException($"Unknown syslog level: 0x{rawLevel:x2}");
        }

        return new SyslogEntry(
            Pid: pid,
            Timestamp: timestamp,
            Level: level,
            ImageName: imageName,
            ImageOffset: senderImageOffset,
            Filename: filename,
            Message: message,
            Label: label,
            ProcId: procId,
            ThreadId: threadId,
            ImageUuid: imageUuid,
            ProcessImageUuid: processImageUuid,
            MachTimestamp: machTimestamp);
    }

    /// <summary>
    /// Collect the device's system logs into a <c>.logarchive</c> that can be inspected later with tools such as
    /// <c>log</c> or Console. Fetches the tar stream via <see cref="CreateArchiveAsync"/> into a temporary file,
    /// then extracts it into <paramref name="outputDirectory"/>.
    /// </summary>
    /// <param name="outputDirectory">Destination directory into which the archive is extracted (created if missing).</param>
    /// <param name="sizeLimit">Optional maximum archive size in bytes.</param>
    /// <param name="ageLimit">Optional maximum age, in days, of entries to include.</param>
    /// <param name="startTime">Optional earliest entry time, as a unix timestamp.</param>
    public async Task CollectAsync(
        string outputDirectory,
        long? sizeLimit = null,
        long? ageLimit = null,
        long? startTime = null,
        CancellationToken cancellationToken = default) {
        DirectoryInfo tempDir = Directory.CreateTempSubdirectory("os_trace_");
        try {
            string tarPath = Path.Combine(tempDir.FullName, "foo.tar");
            await using (FileStream file = File.Create(tarPath)) {
                await CreateArchiveAsync(file, sizeLimit, ageLimit, startTime, cancellationToken).ConfigureAwait(false);
            }

            Directory.CreateDirectory(outputDirectory);
            await TarFile.ExtractToDirectoryAsync(tarPath, outputDirectory, overwriteFiles: true, cancellationToken)
                .ConfigureAwait(false);
        }
        finally {
            tempDir.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Request a log archive from the device and stream its raw bytes into <paramref name="output"/>.
    /// The device returns the archive as a PAX-format tar stream (the contents of <c>/var/db/diagnostics</c>),
    /// written to <paramref name="output"/> in chunks until the connection is terminated.
    /// Use <see cref="CollectAsync"/> instead to obtain an extracted <c>.logarchive</c> directory.
    /// </summary>
    /// <param name="output">A writable stream to receive the archive bytes.</param>
    /// <param name="sizeLimit">Optional maximum archive size in bytes.</param>
    /// <param name="ageLimit">Optional maximum age, in days, of entries to include.</param>
    /// <param name="startTime">Optional earliest entry time, as a unix timestamp.</param>
    /// <exception cref="OsTraceException">The device did not acknowledge the request with a successful status.</exception>
    public async Task CreateArchiveAsync(
        Stream output,
        long? sizeLimit = null,
        long? ageLimit = null,
        long? startTime = null,
        CancellationToken cancellationToken = default
    ) {
        if (Service is null) {
            throw new OsTraceException("Service is null");
        }

        DictionaryNode request = new DictionaryNode() {
            { "Request", new StringNode("CreateArchive") }
        };

        if (sizeLimit is not null) {
            request.Add("SizeLimit", new IntegerNode(sizeLimit.Value));
        }

        if (ageLimit is not null) {
            request.Add("AgeLimit", new IntegerNode(ageLimit.Value));
        }

        if (startTime is not null) {
            request.Add("StartTime", new IntegerNode(startTime.Value));
        }

        await Service.SendPlistAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);

        byte[] response = await Service.ReceiveAsync(1, cancellationToken).ConfigureAwait(false);
        if (response[0] != 1) {
            throw new OsTraceException($"Got invalid response: {Convert.ToHexStringLower(response)}");
        }

        PropertyNode? statusNode = await Service.ReceivePlistAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        DictionaryNode statusPlist = statusNode?.AsDictionaryNode() ?? [];
        if (statusPlist.TryGetValue("Status", out PropertyNode? status) && status.AsStringNode().Value != "RequestSuccessful") {
            throw new OsTraceException($"Got invalid status: {status.AsStringNode().Value ?? "None"}");
        }

        while (true) {
            byte[] magic;
            try {
                magic = await Service.ReceiveAsync(1, cancellationToken).ConfigureAwait(false);
            }
            catch (ConnectionTerminatedException) {
                break;
            }

            if (magic[0] != 3) {
                throw new OsTraceException($"Got invalid magic: {Convert.ToHexStringLower(magic)}");
            }

            byte[] chunk = await Service.ReceivePrefixedAsync(cancellationToken).ConfigureAwait(false);
            await output.WriteAsync(chunk, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Request the device's process list.
    /// </summary>
    /// <returns>The decoded <c>PidList</c> response plist. Its <c>Payload</c> entry maps each pid to a
    /// dictionary of per-process metadata (such as <c>ProcessName</c>).</returns>
    public async Task<DictionaryNode> GetPidListAsync(CancellationToken cancellationToken = default) {
        if (Service is null) {
            throw new OsTraceException("Service is null");
        }

        DictionaryNode request = new DictionaryNode() {
            { "Request", new StringNode("PidList") },
        };
        await Service.SendPlistAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);

        // ignore first received unknown byte
        await Service.ReceiveAsync(1, cancellationToken).ConfigureAwait(false);

        PropertyNode? responseNode = await Service.ReceivePlistAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        return responseNode?.AsDictionaryNode() ?? [];
    }

    /// <summary>
    /// Stream live syslog entries from the device. Starts an activity stream and yields each incoming record,
    /// parsed via <see cref="ParseSyslogEntry"/>, until the caller stops iterating, the token is
    /// cancelled, or the connection drops.
    /// </summary>
    /// <param name="pid">Restrict the stream to a single process; -1 (the default) streams all processes.</param>
    /// <param name="messageFilter">Bitmask selecting which message levels to receive; defaults to all levels.</param>
    /// <param name="streamFlags">Flags controlling stream contents and decoding.</param>
    /// <exception cref="ProtocolException">The device rejected the stream-start request.</exception>
    public async IAsyncEnumerable<SyslogEntry> SyslogAsync(
        int pid = -1,
        int messageFilter = MessageFilterAll,
        OsActivityStreamFlag streamFlags = DefaultStreamFlags,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    ) {
        if (Service is null) {
            throw new OsTraceException("Service is null");
        }

        DictionaryNode request = new DictionaryNode() {
            { "Request", new StringNode("StartActivity") },
            { "MessageFilter", new IntegerNode(messageFilter) },
            { "Pid", new IntegerNode(pid) },
            { "StreamFlags", new IntegerNode((long) streamFlags) }
        };
        await Service.SendPlistAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);

        // A u32 LE gives the width of the length field, which is itself little-endian.
        byte[] lengthLengthBytes = await Service.ReceiveAsync(4, cancellationToken).ConfigureAwait(false);
        int lengthLength = checked((int) BinaryPrimitives.ReadUInt32LittleEndian(lengthLengthBytes));
        byte[] lengthBytes = await Service.ReceiveAsync(lengthLength, cancellationToken).ConfigureAwait(false);
        long length = 0;
        for (int i = lengthBytes.Length - 1; i >= 0; i--) {
            length = (length << 8) | lengthBytes[i];
        }

        byte[] responseBytes = await Service.ReceiveAsync(checked((int) length), cancellationToken).ConfigureAwait(false);
        DictionaryNode response = PropertyList.LoadFromByteArray(responseBytes).AsDictionaryNode();
        if (response.TryGetValue("Status", out PropertyNode? value) && value.AsStringNode().Value != "RequestSuccessful") {
            throw new OsTraceException($"got invalid response: {PropertyList.SaveAsString(response, PlistFormat.Xml)}");
        }

        while (true) {
            byte[] magic = await Service.ReceiveAsync(1, cancellationToken).ConfigureAwait(false);
            if (magic[0] != 0x02) {
                throw new OsTraceException($"got invalid magic: {Convert.ToHexStringLower(magic)}");
            }

            byte[] lineLengthBytes = await Service.ReceiveAsync(4, cancellationToken).ConfigureAwait(false);
            int lineLength = checked((int) BinaryPrimitives.ReadUInt32LittleEndian(lineLengthBytes));

            byte[] line = await Service.ReceiveAsync(lineLength, cancellationToken).ConfigureAwait(false);
            yield return ParseSyslogEntry(line);
        }
    }
}

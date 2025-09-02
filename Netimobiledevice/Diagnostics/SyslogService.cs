using Microsoft.Extensions.Logging;
using Netimobiledevice.Lockdown;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Netimobiledevice.Diagnostics;

/// <summary>
/// Reads a stream of the raw system logs
/// </summary>
public sealed class SyslogService : LockdownService
{
    private const int CHUNK_SIZE = 4096;

    private const string LOCKDOWN_SERVICE_NAME = "com.apple.syslog_relay";
    private const string RSD_SERVICE_NAME = "com.apple.syslog_relay.shim.remote";

    /// <summary>
    /// Used to split each syslog line is "\n" as bytes followed by a null value.
    /// </summary>
    private static readonly byte[] syslogLineSplitter = [0x0A, 0x00];

    public SyslogService(LockdownServiceProvider lockdown, ILogger? logger = null) : base(lockdown, RSD_SERVICE_NAME, logger: logger) { }

    public SyslogService(LockdownClient lockdown, ILogger? logger = null) : base(lockdown, LOCKDOWN_SERVICE_NAME, logger: logger) { }

    private static string DecodeLine(byte[] line)
    {
        return Encoding.UTF8.GetString(line);
    }

    public IEnumerable<string> Watch()
    {
        List<byte> buffer = [];
        while (true) {
            // Read in chunks till we have at least one syslog line
            byte[] chunk = Service.Receive(CHUNK_SIZE);
            buffer.AddRange(chunk);

            // We can split syslog lines based on a 0x00 (null char) preceeded by a '\n'
            // so work out where 0x00 first
            int index = -1;
            for (int i = 0; i < buffer.Count; i++) {
                if (buffer.Skip(i).Take(syslogLineSplitter.Length).SequenceEqual(syslogLineSplitter)) {
                    index = i;
                    break;
                }
            }

            if (index != -1) {
                byte[] line = [.. buffer.Take(index)];
                yield return DecodeLine(line);

                // Clean-up by removing what we've printed out.
                buffer.RemoveRange(0, index + syslogLineSplitter.Length);
            }
        }
    }

    public async IAsyncEnumerable<string> WatchAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        List<byte> buffer = [];
        while (!cancellationToken.IsCancellationRequested) {
            // Read in chunks till we have at least one syslog line
            byte[] chunk = await Service.ReceiveAsync(CHUNK_SIZE, cancellationToken).ConfigureAwait(false);
            buffer.AddRange(chunk);

            // We can split syslog lines based on a 0x00 (null char) preceeded by a '\n'
            // so work out where 0x00 first
            int index = -1;
            for (int i = 0; i < buffer.Count; i++) {
                if (buffer.Skip(i).Take(syslogLineSplitter.Length).SequenceEqual(syslogLineSplitter)) {
                    index = i;
                    break;
                }
            }

            if (index != -1) {
                byte[] line = [.. buffer.Take(index)];
                yield return DecodeLine(line);

                // Clean-up by removing what we've printed out.
                buffer.RemoveRange(0, index + syslogLineSplitter.Length);
            }
        }
    }
}

using Microsoft.Extensions.Logging;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Services.Syslog;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Netimobiledevice.Services;

/// <summary>
/// Stream the device's live system log via the Syslog service.
/// </summary>
public sealed class SyslogService(
    LockdownServiceProvider serviceProvider,
     ILogger? logger = null
) : LockdownService(
    serviceProvider,
    serviceProvider is LockdownClient ? LockdownServiceName : RemoteServiceName,
    logger: logger
) {
    private const int ChunkSize = 4096;

    public const string LockdownServiceName = "com.apple.syslog_relay";
    public const string RemoteServiceName = "com.apple.syslog_relay.shim.remote";

    private static UTF8Encoding StrictUtf8 { get; } = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static ReadOnlySpan<byte> SyslogLineSplitter => "\n\0"u8;

    /// <summary>
    /// Appends every complete, non-empty line in <paramref name="data"/> to <paramref name="output"/>
    /// and returns how many bytes were consumed (up to and including the last delimiter).
    /// </summary>
    private static int ExtractLines(ReadOnlySpan<byte> data, List<string> output) {
        int consumed = 0;

        while (true) {
            int index = data[consumed..].IndexOf(SyslogLineSplitter);
            if (index < 0) {
                break;
            }

            if (index > 0) {
                output.Add(StrictUtf8.GetString(data.Slice(consumed, index)));
            }

            consumed += index + SyslogLineSplitter.Length;
        }

        return consumed;
    }

    /// <summary>
    /// Stream syslog lines from the device as they are emitted.
    /// </summary>
    /// <remarks>
    /// Reads the relay in chunks, splits on the syslog line delimiter, buffers any partial trailing
    /// line until the rest arrives, and decodes each complete line. Empty lines are skipped.
    /// </remarks>
    /// <returns>
    /// Each decoded syslog line (without the trailing delimiter).
    /// </returns>
    /// <exception cref="ConnectionTerminatedException">If the device closes the connection.</exception>
    public async IAsyncEnumerable<string> WatchAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    ) {
        byte[] buffer = new byte[ChunkSize * 2];
        int length = 0;

        List<string> lines = [];
        while (true) {
            if (Service is null) {
                throw new SyslogException("Service is null");
            }

            // read in chunks till we have at least one syslog line
            byte[] chunk = await Service.ReceiveAsync(ChunkSize, cancellationToken).ConfigureAwait(false);
            if (chunk.Length == 0) {
                throw new ConnectionTerminatedException();
            }

            if (length + chunk.Length > buffer.Length) {
                Array.Resize(ref buffer, Math.Max(buffer.Length * 2, length + chunk.Length));
            }

            chunk.CopyTo(buffer, length);
            length += chunk.Length;

            // SyslogLineSplitter is used to split each syslog line
            int consumed = ExtractLines(buffer.AsSpan(0, length), lines);

            // Keep any partial last line for the next read
            if (consumed > 0) {
                Buffer.BlockCopy(buffer, consumed, buffer, 0, length - consumed);
                length -= consumed;
            }

            foreach (string line in lines) {
                yield return line;
            }

            lines.Clear();
        }
    }
}

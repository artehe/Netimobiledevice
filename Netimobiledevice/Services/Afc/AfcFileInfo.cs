using System;
using System.Collections.Generic;
using System.Globalization;

namespace Netimobiledevice.Services.Afc;

/// <summary>
/// Result of <see cref="AfcService.StatAsync"/>: the raw <c>GET_FILE_INFO</c> attributes plus typed
/// accessors for the fields the Python version converted (sizes to ints, nanosecond timestamps to dates).
/// </summary>
public sealed class AfcFileInfo {
    internal AfcFileInfo(Dictionary<string, string> raw) {
        Raw = raw;
        Size = AfcProtocol.ParseLong(raw["st_size"]);
        Blocks = AfcProtocol.ParseLong(raw["st_blocks"]);
        NLink = AfcProtocol.ParseLong(raw["st_nlink"]);
        MTime = FromNanoseconds(AfcProtocol.ParseLong(raw["st_mtime"]));
        BirthTime = FromNanoseconds(AfcProtocol.ParseLong(raw["st_birthtime"]));
    }

    /// <summary>Every attribute exactly as the device sent it.</summary>
    public IReadOnlyDictionary<string, string> Raw { get; }

    public long Size { get; }

    public long Blocks { get; }

    public long NLink { get; }

    public DateTimeOffset MTime { get; }

    public DateTimeOffset BirthTime { get; }

    /// <summary><c>st_ifmt</c>, e.g. <c>S_IFREG</c>, <c>S_IFDIR</c>, <c>S_IFLNK</c>.</summary>
    public string? FileType => Raw.GetValueOrDefault("st_ifmt");

    /// <summary>Target of a symbolic link (<c>LinkTarget</c>), if this is one.</summary>
    public string? LinkTarget => Raw.GetValueOrDefault("LinkTarget");

    public bool IsDirectory => FileType == "S_IFDIR";

    public bool IsRegularFile => FileType == "S_IFREG";

    public bool IsSymlink => FileType == "S_IFLNK";

    public string this[string key] => Raw[key];

    /// <summary>Attributes in device order with converted values.</summary>
    public IEnumerable<KeyValuePair<string, string>> ToDisplayEntries() {
        foreach ((string? key, string? value) in Raw) {
            string display = key switch {
                "st_size" => Size.ToString(CultureInfo.InvariantCulture),
                "st_blocks" => Blocks.ToString(CultureInfo.InvariantCulture),
                "st_nlink" => NLink.ToString(CultureInfo.InvariantCulture),
                "st_mtime" => FormatDateTime(MTime),
                "st_birthtime" => FormatDateTime(BirthTime),
                _ => value,
            };
            yield return new KeyValuePair<string, string>(key, display);
        }
    }

    private static DateTimeOffset FromNanoseconds(long ns) => DateTimeOffset.UnixEpoch.AddTicks(ns / 100);

    private static string FormatDateTime(DateTimeOffset value) {
        DateTime local = value.LocalDateTime;
        return local.Ticks % TimeSpan.TicksPerSecond / 10 == 0
            ? local.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
            : local.ToString("yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture);
    }
}

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Netimobiledevice.Plist;
using System;
using System.Globalization;

namespace Netimobiledevice.Backup;

/// <summary>
/// Represents the Status.plist of a backup
/// </summary>
public class BackupStatus
{
    /// <summary>
    /// The backup unique identifier.
    /// </summary>
    public string UUID { get; set; } = Guid.NewGuid().ToString();
    /// <summary>
    /// The backup timestamp.
    /// </summary>
    public DateTime Date { get; set; } = DateTime.Now;
    /// <summary>
    /// Indicates whether the backup is a full one or incremental.
    /// </summary>
    public bool IsFullBackup { get; set; }
    /// <summary>
    /// Version of the backup protocol.
    /// </summary>
    public Version Version { get; set; } = new Version(3, 3);
    /// <summary>
    /// The backup state.
    /// </summary>
    public BackupState BackupState { get; set; } = BackupState.New;
    /// <summary>
    /// The snapshot state.
    /// </summary>
    public SnapshotState SnapshotState { get; set; } = SnapshotState.Finished;

    public static BackupStatus ParsePlist(DictionaryNode status, ILogger? logger = null)
    {
        logger ??= NullLogger.Instance;

        BackupStatus backupStatus = new BackupStatus {
            UUID = status["UUID"].AsStringNode().Value,
            Date = status["Date"].AsDateNode().Value,
            Version = Version.Parse(status["Version"].AsStringNode().Value),
            IsFullBackup = status["IsFullBackup"].AsBooleanNode().Value
        };

        CultureInfo cultureInfo = CultureInfo.InvariantCulture;
        TextInfo textInfo = cultureInfo.TextInfo;

        string backupStateString = textInfo.ToTitleCase(status["BackupState"].AsStringNode().Value);
        if (Enum.TryParse(backupStateString, out BackupState state)) {
            backupStatus.BackupState = state;
        }
        else {
            logger.LogWarning("New Backup state found: {state}", backupStateString);
        }

        string snapshotStateString = textInfo.ToTitleCase(status["SnapshotState"].AsStringNode().Value);
        if (Enum.TryParse(snapshotStateString, out SnapshotState snapshotState)) {
            backupStatus.SnapshotState = snapshotState;
        }
        else {
            logger.LogWarning("New Snapshot state found: {state}", snapshotStateString);
        }

        return backupStatus;
    }

    public DictionaryNode ToPlist()
    {
        return new DictionaryNode() {
            { "BackupState", new StringNode(BackupState.ToString().ToLowerInvariant()) },
            { "Date", new DateNode(Date) },
            { "IsFullBackup", new BooleanNode(IsFullBackup) },
            { "Version", new StringNode(Version.ToString(2)) },
            { "SnapshotState", new StringNode(SnapshotState.ToString().ToLowerInvariant()) },
            { "UUID", new StringNode(UUID) }
        };
    }
}

using Netimobiledevice.Plist;
using System;

namespace Netimobiledevice.Services.Mobilebackup2;

/// <summary>
/// Represents the Status.plist of a backup
/// </summary>
internal class BackupStatus {
    /// <summary>
    /// The backup unique identifier.
    /// </summary>
    public string UUID { get; set; } = Guid.NewGuid().ToString().ToUpperInvariant();
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

    public DictionaryNode ToPlist() {
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

using Microsoft.Extensions.Logging;
using Netimobiledevice.Plist;
using System;
using System.Globalization;

namespace Netimobiledevice.Backup
{
    /// <summary>
    /// Represents the Status.plist of a backup
    /// </summary>
    public class BackupStatus
    {
        /// <summary>
        /// The backup unique identifier.
        /// </summary>
        public string UUID { get; }
        /// <summary>
        /// The backup timestamp.
        /// </summary>
        public DateTime Date { get; }
        /// <summary>
        /// Indicates whether the backup is a full one or incremental.
        /// </summary>
        public bool IsFullBackup { get; }
        /// <summary>
        /// Version of the backup protocol.
        /// </summary>
        public Version Version { get; }
        /// <summary>
        /// The backup state.
        /// </summary>
        public BackupState BackupState { get; }
        /// <summary>
        /// The snapshot state.
        /// </summary>
        public SnapshotState SnapshotState { get; }

        /// <summary>
        /// Creates an instance of BackupStatus.
        /// </summary>
        /// <param name="status">The dictionary from the Status.plist file.</param>
        public BackupStatus(DictionaryNode status, ILogger logger)
        {
            UUID = status["UUID"].AsStringNode().Value;
            Date = status["Date"].AsDateNode().Value;
            Version = Version.Parse(status["Version"].AsStringNode().Value);
            IsFullBackup = status["IsFullBackup"].AsBooleanNode().Value;

            CultureInfo cultureInfo = CultureInfo.InvariantCulture;
            TextInfo textInfo = cultureInfo.TextInfo;

            string backupStateString = textInfo.ToTitleCase(status["BackupState"].AsStringNode().Value);
            if (Enum.TryParse(backupStateString, out BackupState state)) {
                BackupState = state;
            }
            else {
                logger.LogWarning($"WARNING: New Backup state found: {backupStateString}");
            }

            string snapshotStateString = textInfo.ToTitleCase(status["SnapshotState"].AsStringNode().Value);
            if (Enum.TryParse(snapshotStateString, out SnapshotState snapshotState)) {
                SnapshotState = snapshotState;
            }
            else {
                logger.LogWarning($"WARNING: New Snapshot state found: {snapshotStateString}");
            }
        }
    }
}

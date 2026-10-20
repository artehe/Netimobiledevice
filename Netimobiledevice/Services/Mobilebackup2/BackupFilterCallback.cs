namespace Netimobiledevice.Services.Mobilebackup2;

/// <summary>Predicate deciding whether a single backup file should be preserved.</summary>
public delegate bool BackupFilterCallback(BackupFile backupFile);

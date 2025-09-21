namespace YukkuDock.Core.Models;

public interface IBackupable
{
	string InstalledPath { get; set; }

	bool IsIgnoredBackup { get; set; }
	string BackupPath { get; set; }
}

namespace YukkuDock.Core.Models;

public record ItemTemplate : ProfileEntityBase, IBackupable
{
	public string InstalledPath { get; set; } = string.Empty;

	public bool IsEnabled { get; set; } = true;
	public string MovedPath { get; set; } = string.Empty;

	public bool IsIgnoredBackup { get; set; } = false;
	public string BackupPath { get; set; } = string.Empty;
}

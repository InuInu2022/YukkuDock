namespace YukkuDock.Core.Models;

public record PluginPack : ProfileEntityBase, IBackupable, IActivatable
{
	public string Version { get; set; } = string.Empty;
	public string Author { get; set; } = string.Empty;
	public string InstalledPath { get; set; } = string.Empty;

	public bool IsEnabled { get; set; } = true;
	public string MovedPath { get; set; } = string.Empty;
	public bool IsIgnoredBackup { get; set; }
	public string BackupPath { get; set; } = string.Empty;
}

namespace YukkuDock.Core.Models;

public record VoiceCharacter : IBackupable
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public Guid ProfileId { get; set; }
	public string Name { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public string InstalledPath { get; set; } = string.Empty;

	public bool IsEnabled { get; set; } = true;
	public string MovedPath { get; set; } = string.Empty;

	public bool IsIgnoredBackup { get; set; }
	public string BackupPath { get; set; } = string.Empty;
}

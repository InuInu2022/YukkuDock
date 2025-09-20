using System.Diagnostics.CodeAnalysis;

using Epoxy;

using YukkuDock.Core.Models;

namespace YukkuDock.Desktop.ViewModels;

[ViewModel]
public class PluginPackViewModel(PluginPack pluginPack)
{
	public PluginPack PluginPack { get; } = pluginPack;

	public bool IsEnabled { get; set; } = pluginPack.IsEnabled;
	public string Name { get; set; } = pluginPack.Name;
	public string Version { get; set; } = pluginPack.Version?.ToString() ?? "？";
	public string Author { get; set; } = pluginPack.Author;

	public string InstalledPath { get; set; } = pluginPack.InstalledPath;

	public string FolderName { get; set; } = pluginPack.FolderName;
	public string MovedPath { get; set; } = pluginPack.MovedPath;
	public bool IsIgnoredBackup { get; set; } = pluginPack.IsIgnoredBackup;
	public string BackupPath { get; set; } = pluginPack.BackupPath;

	public string LastWriteTimeUtc { get; set; } = pluginPack.LastWriteTimeUtc.ToString("u");

	[PropertyChanged(nameof(IsEnabled))]
	[SuppressMessage("", "IDE0051")]
	private ValueTask IsEnabledChangedAsync(bool value)
	{
		PluginPack.IsEnabled = value;
		return default;
	}

	[PropertyChanged(nameof(Name))]
	[SuppressMessage("", "IDE0051")]
	private ValueTask NameChangedAsync(string value)
	{
		PluginPack.Name = value;
		return default;
	}

	[PropertyChanged(nameof(Version))]
	[SuppressMessage("", "IDE0051")]
	private ValueTask VersionChangedAsync(string value)
	{
		PluginPack.Version = System.Version.TryParse(value, out var v) ? v : null;
		return default;
	}

	[PropertyChanged(nameof(Author))]
	[SuppressMessage("", "IDE0051")]
	private ValueTask AuthorChangedAsync(string value)
	{
		PluginPack.Author = value;
		return default;
	}
	[PropertyChanged(nameof(InstalledPath))]
	[SuppressMessage("", "IDE0051")]
	private ValueTask InstalledPathChangedAsync(string value)
	{
		PluginPack.InstalledPath = value;
		return default;
	}

	[PropertyChanged(nameof(FolderName))]
	[SuppressMessage("","IDE0051")]
	private ValueTask FolderNameChangedAsync(string value)
	{
		PluginPack.FolderName = value;
		return default;
	}

	[PropertyChanged(nameof(MovedPath))]
	[SuppressMessage("", "IDE0051")]
	private ValueTask MovedPathChangedAsync(string value)
	{
		PluginPack.MovedPath = value;
		return default;
	}

	[PropertyChanged(nameof(BackupPath))]
	[SuppressMessage("", "IDE0051")]
	private ValueTask BackupPathChangedAsync(string value)
	{
		PluginPack.BackupPath = value;
		return default;
	}

	[PropertyChanged(nameof(IsIgnoredBackup))]
	[SuppressMessage("", "IDE0051")]
	private ValueTask IsIgnoredBackupChangedAsync(bool value)
	{
		PluginPack.IsIgnoredBackup = value;
		return default;
	}

	/// <summary>
	/// モデルからViewModelを更新
	/// </summary>
	public void UpdateFromModel(PluginPack updatedPlugin)
	{
		// プロパティの変更通知を発生させて更新
		Name = updatedPlugin.Name;
		Version = updatedPlugin.Version?.ToString() ?? "？";
		Author = updatedPlugin.Author;
		IsEnabled = updatedPlugin.IsEnabled;

		// 内部モデルも更新
		PluginPack.Name = updatedPlugin.Name;
		PluginPack.Version = updatedPlugin.Version;
		PluginPack.Author = updatedPlugin.Author;
	}
}

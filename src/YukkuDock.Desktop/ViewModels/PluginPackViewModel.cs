using System.Diagnostics.CodeAnalysis;
using Epoxy;
using YukkuDock.Core.Models;

namespace YukkuDock.Desktop.ViewModels;

[ViewModel]
public class PluginPackViewModel
{
	public string Name { get; set; }
	public string Author { get; set; }
	public Version? Version { get; set; }
	public string InstalledPath { get; set; }
	public string FolderName { get; set; }
	public DateTime LastWriteTimeUtc { get; set; }
	public bool IsIgnoredBackup { get; set; }
	public bool IsEnabled { get; set; }
	public string LastWriteTimeText => LastWriteTimeUtc.ToString("u");

	public PluginPackViewModel(PluginPack pack)
	{
		Name = pack.Name;
		Author = pack.Author;
		Version = pack.Version;
		InstalledPath = pack.InstalledPath;
		FolderName = pack.FolderName;
		LastWriteTimeUtc = pack.LastWriteTimeUtc;
		IsEnabled = pack.IsEnabled;
		IsIgnoredBackup = pack.IsIgnoredBackup;
	}

	// 詳細情報でプロパティを更新
	public void UpdateFromPluginPack(PluginPack pack)
	{
		Name = pack.Name;
		Author = pack.Author;
		Version = pack.Version;
		LastWriteTimeUtc = pack.LastWriteTimeUtc;
		IsEnabled = pack.IsEnabled;
		IsIgnoredBackup = pack.IsIgnoredBackup;
	}
}

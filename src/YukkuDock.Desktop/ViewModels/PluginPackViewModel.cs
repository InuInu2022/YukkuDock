using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Epoxy;

using YukkuDock.Core;
using YukkuDock.Core.Models;

namespace YukkuDock.Desktop.ViewModels;

[ViewModel]
public class PluginPackViewModel
{
	private readonly PluginPack _pack;


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
		_pack = pack;
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

	[PropertyChanged(nameof(IsEnabled))]
	[SuppressMessage("","IDE0051")]
	private async ValueTask IsEnabledChangedAsync(bool value)
	{
		var result = await PluginManager.TryChangeStatusPluginAsync(_pack, value)
			.ConfigureAwait(true);
		if (!result.Success) {
			Debug.WriteLine(result.Exception?.Message);
		}
	}
}

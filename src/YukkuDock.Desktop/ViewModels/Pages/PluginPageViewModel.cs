using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Epoxy;
using YukkuDock.Core.Models;

namespace YukkuDock.Desktop.ViewModels;

[ViewModel]
public class PluginPageViewModel : IDisposable
{
	private bool _disposedValue;

	public ProfileViewModel? ProfileVm { get; set; }

	public FlatTreeDataGridSource<PluginPack>? PluginsSource { get; private set; }

	ObservableCollection<PluginPack>? _plugins { get; set; }

	public PluginPageViewModel()
	{

	}

	[MemberNotNull(nameof(_plugins))]
	private void InitializePlugins()
	{
		if(ProfileVm is null) return;

		_plugins = new ObservableCollection<PluginPack>(ProfileVm.PluginPacks);

		PluginsSource = new FlatTreeDataGridSource<PluginPack>(_plugins)
		{
			Columns =
			{
				new TextColumn<PluginPack, bool>("IsEnabled", x => x.IsEnabled),
				new TextColumn<PluginPack, string>("Name", x => x.Name),
				new TextColumn<PluginPack, string>("Version", x => x.Version),
				new TextColumn<PluginPack, string>("Author", x => x.Author),
			},
		};
	}

	[PropertyChanged(nameof(ProfileVm))]
	[SuppressMessage("","IDE0051")]
	private ValueTask ProfileVmChangedAsync(ProfileViewModel? value)
	{
		if (value is not null)
		{
			ProfileVm = value;
			InitializePlugins();
		}

		return default;
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!_disposedValue)
		{
			if (disposing)
			{
				PluginsSource?.Dispose();
			}

			// アンマネージド リソース (アンマネージド オブジェクト) を解放し、ファイナライザーをオーバーライドします
			// 大きなフィールドを null に設定します
			_disposedValue = true;
		}
	}

	// 'Dispose(bool disposing)' にアンマネージド リソースを解放するコードが含まれる場合にのみ、ファイナライザーをオーバーライドします
	// ~PluginPageViewModel()
	// {
	//     // このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
	//     Dispose(disposing: false);
	// }

	public void Dispose()
	{
		// このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}

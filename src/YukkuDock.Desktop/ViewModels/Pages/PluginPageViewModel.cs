using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Epoxy;
using YukkuDock.Core.Models;

namespace YukkuDock.Desktop.ViewModels;

[SuppressMessage("Correctness", "SS004:")]
[ViewModel]
public class PluginPageViewModel : IDisposable
{
	private bool _disposedValue;

	public ProfileViewModel? ProfileVm { get; set; }

	public FlatTreeDataGridSource<PluginPackViewModel>? PluginsSource { get; private set; }

	public PluginPackViewModel? SelectedPlugin { get; set; }

	ObservableCollection<PluginPackViewModel>? _plugins { get; set; } = [];

	static readonly FuncDataTemplate<PluginPackViewModel> IsEnabledTemplate = new(
		static (_, __) =>
			new ToggleSwitch
			{
				[!ToggleSwitch.IsCheckedProperty] = new Binding(
					nameof(PluginPackViewModel.IsEnabled)
				),
				OffContent = "",
				OnContent = "",
			}
	);
	static readonly FuncDataTemplate<PluginPackViewModel> IsIgnoredBackupTemplate = new(
		static (_, __) =>
			new ToggleSwitch
			{
				[!ToggleSwitch.IsCheckedProperty] = new Binding(
					nameof(PluginPackViewModel.IsIgnoredBackup)
				),
				OffContent = "",
				OnContent = "",
			}
	);

	public PluginPageViewModel() { }

	private void InitializePlugins()
	{
		if (ProfileVm is null)
			return;

		var list = ProfileVm.PluginPacks.Select(p => new PluginPackViewModel(p));
		_plugins = new ObservableCollection<PluginPackViewModel>(list);

		PluginsSource = new FlatTreeDataGridSource<PluginPackViewModel>(_plugins)
		{
			Columns =
			{
				new TemplateColumn<PluginPackViewModel>("有効", IsEnabledTemplate),
				new TextColumn<PluginPackViewModel, string>("プラグイン名", x => x.Name),
				new TextColumn<PluginPackViewModel, string>("バージョン", x => x.Version),
				new TextColumn<PluginPackViewModel, string>("作者", x => x.Author),
				new TemplateColumn<PluginPackViewModel>("バッグアップ可", IsIgnoredBackupTemplate),
			},
		};

		PluginsSource.RowSelection!.SelectionChanged += (s, e) =>
			SelectedPlugin = e.SelectedItems[0];
	}

	[PropertyChanged(nameof(ProfileVm))]
	[SuppressMessage("", "IDE0051")]
	private ValueTask ProfileVmChangedAsync(ProfileViewModel? value)
	{
		if (value is not null)
		{
			ProfileVm = value;
			InitializePlugins();
		}

		return default;
	}

	[PropertyChanged(nameof(SelectedPlugin))]
	[SuppressMessage("", "IDE0051")]
	private ValueTask SelectedPluginChangedAsync(PluginPackViewModel? value)
	{
		if (value is null)
			return default;

		Debug.WriteLine($"Selected Plugin: {value.Name}, {value}");
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

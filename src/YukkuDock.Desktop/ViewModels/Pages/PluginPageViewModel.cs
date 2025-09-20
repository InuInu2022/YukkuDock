using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Loader;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Platform.Storage;
using Epoxy;
using YukkuDock.Core;
using YukkuDock.Core.Models;
using YukkuDock.Desktop.Extensions;
using YukkuDock.Desktop.Views;

namespace YukkuDock.Desktop.ViewModels;

[SuppressMessage("Correctness", "SS004:")]
[ViewModel]
public class PluginPageViewModel : IDisposable
{
	private bool _disposedValue;

	public ProfileViewModel? ProfileVm { get; set; }

	public Pile<PluginPage> PagePile { get; } = Pile.Factory.Create<PluginPage>();

	public FlatTreeDataGridSource<PluginPackViewModel>? PluginsSource { get; private set; }

	public PluginPackViewModel? SelectedPlugin { get; set; }

	public Command? OpenPluginFolderCommand { get; set; }
	public Command? UpdatePluginsCommand { get; set; }
	public bool CanOpenPluginFolder { get; set; }

	ObservableCollection<PluginPackViewModel>? _plugins { get; set; } = [];

	static readonly FuncDataTemplate<PluginPackViewModel> IsEnabledTemplate = new(
		static (_, __) =>
			new Viewbox()
			{
				Width = 64,
				Height = 16,
				Child = new ToggleSwitch
				{
					[!ToggleSwitch.IsCheckedProperty] = new Binding(
						nameof(PluginPackViewModel.IsEnabled)
					),
					OffContent = "",
					OnContent = "",
				},
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

		LoadPluginData();

		PluginsSource.RowSelection!.SelectionChanged += (s, e) =>
			SelectedPlugin = e.SelectedItems[0];
	}

	[MemberNotNull(nameof(PluginsSource))]
	[SuppressMessage("Usage", "SMA0040:Missing Using Statement", Justification = "<保留中>")]

	private void LoadPluginData()
	{
		var list = ProfileVm?.PluginPacks.Select(p => new PluginPackViewModel(p))
			?? [];
		_plugins = new ObservableCollection<PluginPackViewModel>(list);

		PluginsSource = new FlatTreeDataGridSource<PluginPackViewModel>(_plugins)
		{
			Columns =
			{
				new TemplateColumn<PluginPackViewModel>("有効", IsEnabledTemplate),
				new TextColumn<PluginPackViewModel, string>("フォルダ", x => x.FolderName),
				new TextColumn<PluginPackViewModel, string>("プラグイン名", x => x.Name),
				new TextColumn<PluginPackViewModel, string>("バージョン", x => x.Version),
				new TextColumn<PluginPackViewModel, string>("作者", x => x.Author),
				//new TemplateColumn<PluginPackViewModel>("バックアップ可", IsIgnoredBackupTemplate),
			},
		};
	}


	void SetCommands()
	{
		OpenPluginFolderCommand = Command.Factory.CreateEasy(async () =>
		{
			if (ProfileVm is null)
			{
				return;
			}

			if (!PathManager.TryGetPluginFolder(ProfileVm.AppPath, out var folder))
			{
				return;
			}

			await PagePile
				.RentAsync(
					async (page) =>
					{
						var topLevel = TopLevel.GetTopLevel(page);
						if (topLevel is null)
							return;

						await topLevel
							.Launcher.LaunchDirectoryInfoAsync(folder)
							.ConfigureAwait(true);
					}
				)
				.ConfigureAwait(true);
		});

		UpdatePluginsCommand = Command.Factory.CreateEasy(async () =>
		{
			if (ProfileVm is null)
			{
				return;
			}

			if (!PathManager.TryGetPluginFolder(ProfileVm.AppPath, out var folder))
			{
				return;
			}

			var sw = Stopwatch.StartNew();

			ProfileVm.PluginPacks = await PluginManager
				.LoadPluginsFromDirectoryAsync(ProfileVm.AppPath, folder)
				.ConfigureAwait(true);
			LoadPluginData();

			sw.Stop();
			Debug.WriteLine($"Plugin update completed in {sw.ElapsedMilliseconds} ms");
		});
	}

	[PropertyChanged(nameof(ProfileVm))]
	[SuppressMessage("", "IDE0051")]
	private ValueTask ProfileVmChangedAsync(ProfileViewModel? value)
	{
		if (value is not null)
		{
			ProfileVm = value;
			InitializePlugins();
			SetCommands();

			CanOpenPluginFolder = PathManager.TryGetPluginFolder(ProfileVm.AppPath, out _);
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

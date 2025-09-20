using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;


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
public class PluginPageViewModel
{
	public ProfileViewModel? ProfileVm { get; set; }

	public Pile<PluginPage> PagePile { get; } = Pile.Factory.Create<PluginPage>();
	public Well<PluginPage> PageWell { get; } = Well.Factory.Create<PluginPage>();

	public FlatTreeDataGridSource<PluginPackViewModel>? PluginsSource { get; private set; }

	public PluginPackViewModel? SelectedPlugin { get; set; }

	public Command? OpenPluginFolderCommand { get; set; }
	public Command? UpdatePluginsCommand { get; set; }

	public bool CanOpenPluginFolder { get; set; }
	public bool IsUpdatingPlugins { get; set; }

	public int LoadPluginsPerFolder { get; set; } = 10;

	ObservableCollection<PluginPackViewModel>? _plugins { get; set; } = [];

	// プロファイルごとにキャッシュ
	static readonly Dictionary<string, ICollection<PluginPack>> ProfilePluginCache = [];

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

	public PluginPageViewModel()
	{
		PageWell.Add(
			"Loaded",
			async () =>
			{
				await InitializePluginsAsync().ConfigureAwait(true);
			}
		);
	}



	public async ValueTask InitializePluginsAsync()
	{
		if (ProfileVm is null)
			return;

		// AppPathでキャッシュ判定
		if (ProfilePluginCache.TryGetValue(ProfileVm.AppPath, out var cached))
		{
			ProfileVm.PluginPacks = cached;
			LoadPluginData();
			return;
		}

		await UpdatePluginsAsync().ConfigureAwait(true);
	}

	[MemberNotNull(nameof(PluginsSource))]
	[SuppressMessage("Usage", "SMA0040:Missing Using Statement", Justification = "<保留中>")]
	private void LoadPluginData()
	{
		var list = ProfileVm?.PluginPacks.Select(p => new PluginPackViewModel(p)) ?? [];
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

		UpdatePluginsCommand = Command.Factory.Create(
			async () =>
			{
				await UpdatePluginsAsync().ConfigureAwait(true);
			},
			() => !IsUpdatingPlugins
		);
	}

	[PropertyChanged(nameof(ProfileVm))]
	[SuppressMessage("", "IDE0051")]
	async ValueTask ProfileVmChangedAsync(ProfileViewModel? value)
	{
		if (value is null)
			return;

		ProfileVm = value;
		SetCommands();

		CanOpenPluginFolder = PathManager.TryGetPluginFolder(ProfileVm.AppPath, out _);

		// AppPathでキャッシュ判定
		if (ProfilePluginCache.TryGetValue(ProfileVm.AppPath, out var cached))
		{
			ProfileVm.PluginPacks = cached;
			LoadPluginData();
		}
		else
		{
			await UpdatePluginsAsync().ConfigureAwait(true);
		}
	}

	private async ValueTask UpdatePluginsAsync()
	{
		IsUpdatingPlugins = true;
		UpdatePluginsCommand?.ChangeCanExecute();

		if (ProfileVm is null)
		{
			IsUpdatingPlugins = false;
			UpdatePluginsCommand?.ChangeCanExecute();
			return;
		}

		var appPath = ProfileVm.AppPath;
		if (!PathManager.TryGetPluginFolder(appPath, out var folder))
		{
			IsUpdatingPlugins = false;
			UpdatePluginsCommand?.ChangeCanExecute();
			return;
		}

		var sw = Stopwatch.StartNew();

		try
		{
			var pluginPacks = await PluginManager
				.LoadPluginsFromDirectoryAsync(appPath, folder, LoadPluginsPerFolder)
				.ConfigureAwait(false);

			await UIThread.InvokeAsync(() =>
			{
				ProfileVm.PluginPacks = pluginPacks;
				ProfilePluginCache[ProfileVm.AppPath] = pluginPacks;
				LoadPluginData();
				IsUpdatingPlugins = false;
				UpdatePluginsCommand?.ChangeCanExecute();
				return default;
			}).ConfigureAwait(true);
		}
		catch (Exception ex)
		{
			IsUpdatingPlugins = false;
			UpdatePluginsCommand?.ChangeCanExecute();
			Debug.WriteLine($"Plugin update failed: {ex}");
		}

		sw.Stop();
		Debug.WriteLine($"Plugin update completed in {sw.ElapsedMilliseconds} ms");
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

	[PropertyChanged(nameof(IsUpdatingPlugins))]
	[SuppressMessage("", "IDE0051")]
	private ValueTask IsUpdatingPluginsChangedAsync(bool value)
	{
		OpenPluginFolderCommand?.ChangeCanExecute();
		UpdatePluginsCommand?.ChangeCanExecute();
		return default;
	}
}

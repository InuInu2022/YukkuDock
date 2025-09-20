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

	public Pile<TreeDataGrid> PluginListPile { get; } = Pile.Factory.Create<TreeDataGrid>();

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
		SetCommands();

		PageWell.Add("Loaded", async () =>
		{
			if (ProfileVm is null)
				return;

			IsUpdatingPlugins = true;
			LoadPluginData([]);

			// キャッシュ判定
			if (ProfilePluginCache.TryGetValue(ProfileVm.AppPath, out var cached))
			{
				ProfileVm.PluginPacks = cached;
				await UIThread.InvokeAsync(() =>
				{
					LoadPluginData(cached);
					IsUpdatingPlugins = false;
					return default;
				}).ConfigureAwait(true);
				return;
			}

			// 逐次読込
			await UpdatePluginsProgressivelyAsync().ConfigureAwait(true);

			IsUpdatingPlugins = false;
		});
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
	void LoadPluginData(IEnumerable<PluginPack>? pluginPacks = null)
	{
		var source = pluginPacks ?? ProfileVm?.PluginPacks;
		var list = source?.Select(p => new PluginPackViewModel(p)) ?? [];
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
				new TextColumn<PluginPackViewModel, string>("最終更新日時", x => x.LastWriteTimeUtc),
			},
		};
	}

	/// <summary>
	/// プラグインを段階的に更新（基本情報→詳細情報）
	/// </summary>
	[SuppressMessage(
		"Usage",
		"VSTHRD101:Avoid unsupported async delegates",
		Justification = "<保留中>"
	)]
	[SuppressMessage(
		"Usage",
		"MA0147:Avoid async void method for delegate",
		Justification = "<保留中>"
	)]
	[SuppressMessage(
		"Concurrency",
		"PH_S034:Async Lambda Inferred to Async Void",
		Justification = "<保留中>"
	)]
	private async ValueTask UpdatePluginsProgressivelyAsync()
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
			// UI用のコレクションをクリア
			await UIThread
				.InvokeAsync(() =>
				{
					_plugins?.Clear();
					ProfileVm.PluginPacks.Clear();
					return default;
				})
				.ConfigureAwait(false);

			// 段階的ロード用のProgress
			var progress = new Progress<PluginPack>(async plugin =>
			{
				try
				{
					// UIスレッドでコレクション更新
					await UIThread
						.InvokeAsync(() =>
						{
							// 既存アイテムを検索
							var existingVm = _plugins?.FirstOrDefault(p =>
								string.Equals(
									p.InstalledPath,
									plugin.InstalledPath,
									StringComparison.Ordinal
								)
							);

							if (existingVm is not null)
							{
								// 詳細情報が来たらプロパティ更新
								existingVm.Name = plugin.Name;
								existingVm.Author = plugin.Author;
								existingVm.Version = plugin.Version?.ToString() ?? string.Empty;
								// 必要なら他のプロパティも
							}
							else
							{
								// 新規追加
								_plugins?.Add(new PluginPackViewModel(plugin));
							}

							return ValueTask.CompletedTask;
						})
						.ConfigureAwait(true);
				}
				catch (System.Exception ex)
				{
					// エラーハンドリング
					Debug.WriteLine(ex.Message);
				}
			});

			// 段階的ロード実行
			var pluginPacks = await PluginManager
				.LoadPluginsProgressivelyAsync(appPath, folder, LoadPluginsPerFolder, progress)
				.ConfigureAwait(false);

			// キャッシュ・ProfileVmへ反映
			await UIThread.InvokeAsync(() =>
			{
				ProfileVm.PluginPacks = pluginPacks;
				ProfilePluginCache[ProfileVm.AppPath] = pluginPacks;
				IsUpdatingPlugins = false;
				UpdatePluginsCommand?.ChangeCanExecute();
				return default;
			}).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			await UIThread
				.InvokeAsync(() =>
				{
					IsUpdatingPlugins = false;
					UpdatePluginsCommand?.ChangeCanExecute();
					return default;
				})
				.ConfigureAwait(false);
			Debug.WriteLine($"Plugin update failed: {ex}");
		}

		sw.Stop();
		Debug.WriteLine($"Progressive plugin update completed in {sw.ElapsedMilliseconds} ms");
	}

	void SetCommands()
	{
		OpenPluginFolderCommand = Command.Factory.CreateEasy(async () =>
		{
			if (ProfileVm is null)
				return;

			if (!PathManager.TryGetPluginFolder(ProfileVm.AppPath, out var folder))
				return;

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

		// 段階的更新を使用
		UpdatePluginsCommand = Command.Factory.Create(
			async () =>
			{
				await UpdatePluginsProgressivelyAsync().ConfigureAwait(true);
			},
			() => !IsUpdatingPlugins
		);
	}

	[PropertyChanged(nameof(ProfileVm))]
	[SuppressMessage("", "IDE0051")]
	ValueTask ProfileVmChangedAsync(ProfileViewModel? value)
	{
		if (value is null)
			return default;

		ProfileVm = value;


		CanOpenPluginFolder = PathManager.TryGetPluginFolder(ProfileVm.AppPath, out _);

		return default;
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

			await UIThread
				.InvokeAsync(() =>
				{
					ProfileVm.PluginPacks = pluginPacks;
					ProfilePluginCache[ProfileVm.AppPath] = pluginPacks;
					LoadPluginData();
					IsUpdatingPlugins = false;
					UpdatePluginsCommand?.ChangeCanExecute();
					return default;
				})
				.ConfigureAwait(true);
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

	[PropertyChanged(nameof(PluginsSource))]
	[SuppressMessage("","IDE0051")]
	private async ValueTask PluginsSourceChangedAsync(
		FlatTreeDataGridSource<PluginPackViewModel>? value
	)
	{
		if (value is null)
			return;

		await PluginListPile.RentAsync((list) =>
		{
			list.InvalidateVisual();
			list.UpdateLayout();
			return ValueTask.CompletedTask;
		}).ConfigureAwait(true);
	}
}

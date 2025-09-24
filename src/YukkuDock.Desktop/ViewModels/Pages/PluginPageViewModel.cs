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
using YukkuDock.Core.Services;
using YukkuDock.Desktop.Extensions;
using YukkuDock.Desktop.Views;

namespace YukkuDock.Desktop.ViewModels;

[SuppressMessage("Correctness", "SS004:")]
[ViewModel]
public class PluginPageViewModel : IDisposable
{
	public string SubTitle { get; set; } = "プラグイン管理";
	public ProfileViewModel? ProfileVm { get; set; }

	public Pile<PluginPage> PagePile { get; } = Pile.Factory.Create<PluginPage>();
	public Well<PluginPage> PageWell { get; } = Well.Factory.Create<PluginPage>();

	public Pile<TreeDataGrid> PluginListPile { get; } = Pile.Factory.Create<TreeDataGrid>();
	public Well<TreeDataGrid> PluginListWell { get; } = Well.Factory.Create<TreeDataGrid>();

	public FlatTreeDataGridSource<PluginPackViewModel>? PluginsSource { get; private set; }

	public PluginPackViewModel? SelectedPlugin { get; set; }

	public Command? OpenPluginFolderCommand { get; set; }
	public Command? UpdatePluginsCommand { get; set; }

	public Command? BackupPluginPacksCommand { get; set; }

	public bool CanOpenPluginFolder { get; set; }
	public bool CanUpdatePlugins { get; set; } = true;
	public bool IsOpenAllPluginFolder { get; set; } = true;
	public bool IsUpdatingPlugins { get; set; }

	public bool IsBackupAllPlugins { get; set; }

	public int LoadPluginsPerFolder { get; set; } = 10;

	public ObservableCollection<PluginPackViewModel> Plugins { get; } = [];

	// プロファイルごとにキャッシュ
	static readonly Dictionary<string, ICollection<PluginPack>> ProfilePluginCache = [];

	static readonly FuncDataTemplate<PluginPackViewModel> IsEnabledTemplate = new(
		static (_, __) =>
			new Viewbox()
			{
				Width = 36,
				Height = 22,
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

	readonly IProfileService profileService;
	readonly ISettingsService settingsService;
	private bool _disposedValue;


	public PluginPageViewModel(IProfileService profileService, ISettingsService settingsService)
	{
		this.profileService = profileService;
		this.settingsService = settingsService;

		SetCommands();

		PageWell.Add("Loaded", async () =>
		{
			if (ProfileVm is null)
				return;

			IsUpdatingPlugins = true;
			LoadPluginData(ProfileVm.PluginPacks);

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
			if (ProfileVm.PluginPacks is null || ProfileVm.PluginPacks.Count == 0)
			{
				await UpdatePluginsProgressivelyAsync().ConfigureAwait(true);
			}

			// 選択変更イベント登録
			await PluginListPile.RentAsync((list) =>
				{
					if (list.RowSelection is null)
						return default;

					list.RowSelection.SelectionChanged += (s, e) =>
					{
						if (
							list.RowSelection.SelectedItem
							is not PluginPackViewModel selected
						)
						{
							return;
						}

						SelectedPlugin = selected;

						CanOpenPluginFolder =
							IsOpenAllPluginFolder
							? CanOpenPluginFolder
							: SelectedPlugin is not null;

						OpenPluginFolderCommand?.ChangeCanExecute();
					};

					return default;
				}
			)
			.ConfigureAwait(true);

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
		Plugins.Clear();
		foreach (var vm in list)
		{
			Plugins.Add(vm);
		}

		PluginsSource = new FlatTreeDataGridSource<PluginPackViewModel>(Plugins)
		{
			Columns =
			{
				new TemplateColumn<PluginPackViewModel>("有効", IsEnabledTemplate, options:new(){
					CompareAscending = static (x, y) => x?.IsEnabled.CompareTo(y?.IsEnabled) ?? 0,
					CompareDescending = static (x, y) => y?.IsEnabled.CompareTo(x?.IsEnabled) ?? 0,
				} ),
				new TextColumn<PluginPackViewModel, string>("フォルダ", x => x.FolderName),
				new TextColumn<PluginPackViewModel, string>("プラグイン名", x => x.Name),
				new TextColumn<PluginPackViewModel, string>("バージョン", static x => x.Version != null ? x.Version.ToString() : "?"),
				new TextColumn<PluginPackViewModel, string>("最終更新日時", x => x.LastWriteTimeText),
				new TextColumn<PluginPackViewModel, string>("作者", x => x.Author),
			},
		};

		SubTitle = $"プラグイン管理 - ({Plugins.Count} 個)";
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
					Plugins.Clear();
					ProfileVm.PluginPacks.Clear();
					return default;
				})
				.ConfigureAwait(false);
			Progress<PluginPack> progress = CreateProgressForPluginUpdate();

			// 段階的ロード実行
			var profileId = ProfileVm.Profile.Id;
			var pluginPacks = await PluginManager
				.LoadPluginsProgressivelyAsync(appPath, folder, profileId, LoadPluginsPerFolder, progress)
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

	private Progress<PluginPack> CreateProgressForPluginUpdate()
	{
		var progress = new Progress<PluginPack>(plugin =>
		{
			var valueTask = UIThread.InvokeAsync(() =>
			{
				var existingVm = Plugins.FirstOrDefault(p =>
					string.Equals(p.InstalledPath, plugin.InstalledPath, StringComparison.Ordinal)
				);

				if (existingVm is not null)
				{
					existingVm.UpdateFromPluginPack(plugin);
				}
				else
				{
					Plugins.Add(new PluginPackViewModel(plugin));
				}

				return ValueTask.CompletedTask;
			});

			if (valueTask.IsCompletedSuccessfully)
			{
				valueTask.GetAwaiter().GetResult();
			}
			else if (valueTask.IsFaulted)
			{
				Debug.WriteLine("UIThread.InvokeAsyncで例外が発生しました");
			}
		});
		return progress;
	}

	void SetCommands()
	{
		OpenPluginFolderCommand = Command.Factory.CreateEasy(async () =>
		{
			if (ProfileVm is null)
				return;

			if (!PathManager.TryGetPluginFolder(ProfileVm.AppPath, out var folder))
				return;

			if (!IsOpenAllPluginFolder)
			{
				if (SelectedPlugin is null)
					return;

				var pluginFolder = Path.GetDirectoryName(SelectedPlugin.InstalledPath);
				if (pluginFolder is null || !Directory.Exists(pluginFolder))
					return;

				folder = new DirectoryInfo(pluginFolder);
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

		// 段階的更新を使用
		UpdatePluginsCommand = Command.Factory.Create(
			async () =>
			{
				await UpdatePluginsProgressivelyAsync().ConfigureAwait(true);
			},
			() => !IsUpdatingPlugins
		);

		BackupPluginPacksCommand = Command.Factory.Create(async () =>
		{
			if (ProfileVm is null || ProfileVm.PluginPacks is null || ProfileVm.PluginPacks.Count == 0)
				return;

			IsUpdatingPlugins = true;
			UpdatePluginsCommand?.ChangeCanExecute();
			BackupPluginPacksCommand?.ChangeCanExecute();

			List<PluginPack> packs = IsBackupAllPlugins
				? [.. ProfileVm.PluginPacks]
				: SelectedPlugin is null
					? [] : new List<PluginPack>([SelectedPlugin.PluginPack]);

			var result = await BackupManager
				.TryBackupPluginPacksAsync(
					profileService,
					ProfileVm.Profile,
					packs
				)
				.ConfigureAwait(true);

			if (!result.Success)
			{
				Debug.WriteLine(
					"プラグインのバックアップに失敗しました。" + result.Exception?.Message ?? ""
				);
			}

			var folder = new DirectoryInfo(
				profileService.GetPluginPacksBackupFolder(ProfileVm.Profile.Id)
			);

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

			IsUpdatingPlugins = false;
			UpdatePluginsCommand?.ChangeCanExecute();
			BackupPluginPacksCommand?.ChangeCanExecute();
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
		OpenPluginFolderCommand?.ChangeCanExecute();
		CanUpdatePlugins = CanOpenPluginFolder;
		UpdatePluginsCommand?.ChangeCanExecute();

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
	[SuppressMessage("", "IDE0051")]
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

	[PropertyChanged(nameof(IsOpenAllPluginFolder))]
	[SuppressMessage("", "IDE0051")]
	private ValueTask IsOpenAllPluginFolderChangedAsync(bool value)
	{
		CanOpenPluginFolder = value || SelectedPlugin is not null;
		OpenPluginFolderCommand?.ChangeCanExecute();
		return default;
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!_disposedValue)
		{
			if (disposing)
			{
				// マネージド状態を破棄します (マネージド オブジェクト)
				PluginsSource?.Dispose();
			}

			_disposedValue = true;
		}
	}

	// // TODO: 'Dispose(bool disposing)' にアンマネージド リソースを解放するコードが含まれる場合にのみ、ファイナライザーをオーバーライドします
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

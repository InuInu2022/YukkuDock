using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Reflection;

using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Epoxy;
using FluentAvalonia.UI.Controls;
using YukkuDock.Core;
using YukkuDock.Core.Models;
using YukkuDock.Core.Services;
using YukkuDock.Desktop.Extensions;

namespace YukkuDock.Desktop.ViewModels;

[ViewModel]
public partial class MainWindowViewModel
{
	public string WindowTitle {get;set;} = string.Empty;
	public ObservableCollection<ProfileViewModel> Profiles { get; set; }

	public ProfileViewModel? SelectedItem { get; set; }

	public Well<Window> MainWindowWell { get; } = Well.Factory.Create<Window>();
	public Pile<Window> MainWindowPile { get; } = Pile.Factory.Create<Window>();

	public bool IsLoaded { get; set; }

	public bool IsProfileSelected { get; set; }

	public Command? AddCommand { get; private set; }
	public bool IsAddButtonEnabled { get; set; } = true;

	public Command? OpenAppCommand { get; private set; }
	public bool IsOpenAppButtonEnabled { get; set; } = true;

	public Command? EditProfileCommand { get; private set; }

	public Command? OpenProfileFolderCommand { get; private set; }
	public Command? DuplicateProfileCommand { get; private set; }
	public Command? DeleteProfileCommand { get; private set; }

	public Command? BackupProfileCommand { get; private set; }

	readonly ISettingsService settingsService;
	readonly IProfileService profileService;
	Settings? _currentSettings;

	public MainWindowViewModel(ISettingsService settingsService, IProfileService profileService)
	{
		IsLoaded = true;
		this.settingsService = settingsService;
		this.profileService = profileService;
		Profiles = [];

		MainWindowWell.Add(
			"Loaded",
			[MemberNotNull(nameof(_currentSettings))]
			async () =>
			{
				IsLoaded = true;

				WindowTitle = "YukkuDock for YMM4 - " + Assembly.GetEntryAssembly()
						?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
						?.InformationalVersion;

				//load settings
				await LoadSettingsAsync(settingsService).ConfigureAwait(true);

				//load profiles
				await LoadProfilesAsync(profileService).ConfigureAwait(true);

				IsLoaded = false;
			}
		);

		MainWindowWell.Add(
			"Closed",
			async () =>
			{
				// ウィンドウ終了時に設定保存
				if (_currentSettings is null)
					return;

				var result = await settingsService
					.TrySaveAsync(_currentSettings)
					.ConfigureAwait(true);
			}
		);

		InitializeCommands();
	}

	private async ValueTask LoadProfilesAsync(IProfileService profileService)
	{
		var profilesResult = await profileService.TryLoadAllAsync().ConfigureAwait(true);
		if (profilesResult.Success)
		{
			foreach (var profile in profilesResult.Value ?? [])
			{
				var vm = new ProfileViewModel(profile, profileService);
				Profiles.Add(vm);
			}
		}
	}

	[MemberNotNull(nameof(_currentSettings))]
	private async ValueTask LoadSettingsAsync(ISettingsService settingsService)
	{
		var settingsResult = await settingsService.TryLoadAsync().ConfigureAwait(true);
		if (settingsResult.Success && settingsResult.Value is not null)
		{
			//設定復元
			_currentSettings = settingsResult.Value;
			return;
		}

		_currentSettings = new Settings();
	}

	[SuppressMessage("Correctness", "SS002:DateTime.Now was referenced", Justification = "<保留中>")]
	private void InitializeCommands()
	{
		OpenAppCommand = Command.Factory.Create(() =>
		{
			if (SelectedItem == null || string.IsNullOrWhiteSpace(SelectedItem.AppPath))
			{
				return default;
			}
			IsAddButtonEnabled = false;

			try
			{
				using var process = Process.Start(
					new ProcessStartInfo { FileName = SelectedItem.AppPath, UseShellExecute = true }
				);
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex.Message);
				IsAddButtonEnabled = true;
			}
			IsAddButtonEnabled = true;
			return default;
		} /*, () =>
		{
			return IsAddButtonEnabled && (SelectedItem?.IsAppExists ?? false);
		}*/
		);

		AddCommand = Command.Factory.Create(
			async () =>
			{
				IsAddButtonEnabled = false;

				//TODO:show dialog

				var newProfile = new Profile { Name = "新しいプロファイル", Description = "" };
				var saveResult = await profileService.TrySaveAsync(newProfile).ConfigureAwait(true);
				if (saveResult.Success)
				{
					var newProfileViewModel = new ProfileViewModel(newProfile, profileService);
					Profiles.Add(newProfileViewModel);
					SelectedItem = newProfileViewModel;
				}
				await Task.Delay(10).ConfigureAwait(true);
				IsProfileSelected = true;
				IsAddButtonEnabled = true;
			},
			() => IsAddButtonEnabled
		);

		EditProfileCommand = Command.Factory.Create(async () =>
		{
			if (SelectedItem == null)
			{
				return;
			}

			var profileWindow = new ProfileWindow
			{
				DataContext = App.Container?.GetService<ProfileWindowViewModel>(),
			};
			if (profileWindow.DataContext is ProfileWindowViewModel vm)
			{
				vm.ProfileVm = SelectedItem;
			}

			await MainWindowPile
				.RentAsync(async owner =>
				{
					await profileWindow.ShowDialog(owner).ConfigureAwait(true);
				})
				.ConfigureAwait(true);
		});

		DuplicateProfileCommand = Command.Factory.CreateEasy(async () =>
		{
			if (SelectedItem is null)
			{
				return;
			}

			IsProfileSelected = false;
			var dupProfile = SelectedItem.Profile with { Id = Guid.NewGuid() };
			var saveResult = await profileService.TrySaveAsync(dupProfile).ConfigureAwait(true);
			if (saveResult.Success)
			{
				var dupProfileViewModel = new ProfileViewModel(dupProfile, profileService);
				Profiles.Add(dupProfileViewModel);
				SelectedItem = dupProfileViewModel;
			}
			await Task.Delay(10).ConfigureAwait(true);
			IsProfileSelected = true;
		});

		OpenProfileFolderCommand = Command.Factory.CreateEasy(async () =>
		{
			if (SelectedItem is null)
			{
				return;
			}

			var folder = profileService.GetProfileFolder(SelectedItem.Profile.Id);

			await MainWindowPile
				.RentAsync(
					async (window) =>
					{
						var topLevel = TopLevel.GetTopLevel(window);
						if (topLevel is null)
							return;

						await topLevel
							.Launcher.LaunchDirectoryInfoAsync(new(folder))
							.ConfigureAwait(true);
					}
				)
				.ConfigureAwait(true);
		});

		BackupProfileCommand = Command.Factory.CreateEasy(async () =>
		{
			if (SelectedItem is null)
			{
				return;
			}

			var backupFolder = profileService
				.GetProfileBackupFolder(SelectedItem.Profile.Id);


			var result = await BackupManager.TryBackupAsync(
				profileService,
				SelectedItem.Profile
			).ConfigureAwait(true);

			if (!result.Success)
			{
				// ユーザーに通知
				var td = new TaskDialog
				{
					Title = "バックアップエラー",
					IconSource = new SymbolIconSource { Symbol = Symbol.Important },
					Header = $"{result.Exception?.Message ?? "不明なエラー"}",
					SubHeader =
						$"バックアップファイルが作成できません。再度実行してください。\n {result.Exception?.Message ?? ""}",
					ShowProgressBar = false,
					Buttons = { TaskDialogButton.OKButton },
				};

				await MainWindowPile
					.RentAsync(owner =>
					{
						td.XamlRoot = TopLevel.GetTopLevel(owner);
						return default;
					})
					.ConfigureAwait(true);
				await td.ShowAsync().ConfigureAwait(true);
			}

			await MainWindowPile
				.RentAsync(
					async (window) =>
					{
						var topLevel = TopLevel.GetTopLevel(window);
						if (topLevel is null)
							return;

						await topLevel
							.Launcher.LaunchDirectoryInfoAsync(new(backupFolder))
							.ConfigureAwait(true);
					}
				)
				.ConfigureAwait(true);
		});

		DeleteProfileCommand = Command.Factory.CreateEasy(async () =>
		{
			var td = new TaskDialog
			{
				//Title = "プロファイルの削除",
				IconSource = new SymbolIconSource { Symbol = Symbol.Important },
				Header = "ゴミ箱に移動しますか？",
				SubHeader = $"プロファイル 「{SelectedItem?.Name}」をゴミ箱に移動しますか？",
				Content = $"""
				削除プロファイル

				- 名前: {SelectedItem?.Name}
				- YMM4へのパス: {SelectedItem?.AppPath}
				- YMM4バージョン: {SelectedItem?.AppVersion}
				- 説明: {SelectedItem?.Description}
				""",
				ShowProgressBar = false,
				Buttons = { TaskDialogButton.YesButton, TaskDialogButton.NoButton },
			};

			td.Closing += DeleteProfileAsync;

			await MainWindowPile
				.RentAsync(owner =>
				{
					td.XamlRoot = TopLevel.GetTopLevel(owner);
					return default;
				})
				.ConfigureAwait(true);
			await td.ShowAsync().ConfigureAwait(true);
		});
	}

	[SuppressMessage("Usage", "VSTHRD101")]
	[SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "<保留中>")]
	[SuppressMessage(
		"Style",
		"VSTHRD200:Use \"Async\" suffix for async methods",
		Justification = "<保留中>"
	)]
	async void DeleteProfileAsync(object? sender, TaskDialogClosingEventArgs e)
	{
		if ((TaskDialogStandardResult)e.Result == TaskDialogStandardResult.Yes)
		{
			var deferral = e.GetDeferral();
			var td = sender as TaskDialog;
			if (SelectedItem is null || td is null)
			{
				deferral.Complete();
				return;
			}

			td.ShowProgressBar = true;
			td.SetProgressBarState(0, TaskDialogProgressState.Indeterminate);

			_ = await profileService.TryDeleteAsync(SelectedItem.Profile).ConfigureAwait(true);
			Profiles.Remove(SelectedItem);
			SelectedItem = null;
			IsProfileSelected = false;

			deferral.Complete();
		}
		else
		{
			//e.Cancel = true;
		}
	}

	[PropertyChanged(nameof(SelectedItem))]
	[SuppressMessage("", "IDE0051")]
	private ValueTask SelectedItemChangedAsync(ProfileViewModel? value)
	{
		IsProfileSelected = value != null;
		return default;
	}

	[PropertyChanged(nameof(IsAddButtonEnabled))]
	[SuppressMessage("", "IDE0051")]
	private ValueTask IsAddButtonEnabledChangedAsync(bool value)
	{
		AddCommand?.ChangeCanExecute();
		return default;
	}

	[PropertyChanged(nameof(IsOpenAppButtonEnabled))]
	[SuppressMessage("", "IDE0051")]
	private ValueTask IsOpenAppButtonEnabledChangedAsync(bool value)
	{
		OpenAppCommand?.ChangeCanExecute();
		return default;
	}
}

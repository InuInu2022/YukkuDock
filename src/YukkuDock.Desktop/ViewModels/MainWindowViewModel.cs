using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Epoxy;
using YukkuDock.Core.Models;
using YukkuDock.Core.Services;

namespace YukkuDock.Desktop.ViewModels;

[ViewModel]
public partial class MainWindowViewModel
{
	private readonly ISettingsService settingsService;
	private readonly IProfileService profileService;

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

	public MainWindowViewModel(
		ISettingsService settingsService,
		IProfileService profileService
	)
	{
		this.settingsService = settingsService;
		this.profileService = profileService;
		Profiles = [];

		MainWindowWell.Add(
			"Loaded",
			async () =>
			{
				IsLoaded = true;

				//load settings
				await LoadSettingsAsync(settingsService)
					.ConfigureAwait(true);

				//load profiles
				await LoadProfilesAsync(profileService)
					.ConfigureAwait(true);

				IsLoaded = false;
				/* ダミーデータ
				Profiles =
				[
					new(new() { Name = "アイテム0", AppVersion = new Version(4, 44, 1) }),
					new(new() { Name = "アイテム1", AppVersion = new Version(4, 44, 2) }),
					new(new() { Name = "アイテム2", AppVersion = new Version(4, 44, 3) }),
				];

				foreach (var profile in Profiles)
				{
					profile.PluginPacks =
					[
						.. Enumerable
							.Range(0, 30)
							.Select(x => new PluginPack()
							{
								Name = $"プラグイン{x}",
								Version = new Version(x, x, 0),
								Author = $"作者{x}",
							}),
					];
				}
				*/
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
				var vm = new ProfileViewModel(profile);
				Profiles.Add(vm);
			}
		}
	}

	private async ValueTask LoadSettingsAsync(ISettingsService settingsService)
	{
		var settingsResult = await settingsService.TryLoadAsync()
			.ConfigureAwait(true);
		if (settingsResult.Success && settingsResult.Value is { } settings)
		{
			//設定復元
		}
	}

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
				using var process = System.Diagnostics.Process.Start(
					new System.Diagnostics.ProcessStartInfo
					{
						FileName = SelectedItem.AppPath,
						UseShellExecute = true,
					}
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

				var newProfile = new Profile
				{
					Name = "新しいプロファイル",
					Description = "",
				};
				var saveResult = await profileService.TrySaveAsync(newProfile).ConfigureAwait(true);
				if (saveResult.Success)
				{
					var newProfileViewModel = new ProfileViewModel(newProfile);
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

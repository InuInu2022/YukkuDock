using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

using Avalonia.Controls;

using Epoxy;

using YukkuDock.Core.Models;

namespace YukkuDock.Desktop.ViewModels;

[ViewModel]
public partial class MainWindowViewModel
{


	public ObservableCollection<ProfileViewModel> Profiles { get; set; }

	public ProfileViewModel? SelectedItem { get; set; }

	public Well<Window> MainWindowWell { get; }
		= Well.Factory.Create<Window>();
	public Pile<Window> MainWindowPile { get; }
		= Pile.Factory.Create<Window>();

	public bool IsProfileSelected { get; set; }

	public Command? AddCommand { get; private set; }
	public bool IsAddButtonEnabled { get; set; } = true;

	public Command? OpenAppCommand { get; private set; }
	public bool IsOpenAppButtonEnabled { get; set; } = true;

	public Command? EditProfileCommand { get; private set; }

	public MainWindowViewModel()
	{
		Profiles = [];

		MainWindowWell.Add(
			"Loaded",
			() =>
			{
				Profiles =
				[
					new(new() { Name = "アイテム0", AppVersion = new Version(4, 44, 1) }),
					new(new() { Name = "アイテム1", AppVersion = new Version(4, 44, 2) }),
					new(new() { Name = "アイテム2", AppVersion = new Version(4, 44, 3) }),
				];

				return default;
			}
		);

		InitializeCommands();
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
				using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
				{
					FileName = SelectedItem.AppPath,
					UseShellExecute = true,
				});
			}
			catch (Exception ex)
			{
				//
				IsAddButtonEnabled = true;
			}
			IsAddButtonEnabled = true;
			return default;
		}/*, () =>
		{
			return IsAddButtonEnabled && (SelectedItem?.IsAppExists ?? false);
		}*/);

		AddCommand = Command.Factory.Create(async () =>
		{
			IsAddButtonEnabled = false;

			//TODO:show dialog

			var newProfile = new Profile
			{
				Name = "新しいプロファイル",
				AppVersion = new Version(4, 45, 5),
				Description = "ボタンで追加された新しいプロファイルの説明",
			};
			var newProfileViewModel = new ProfileViewModel(newProfile);
			Profiles.Add(newProfileViewModel);
			SelectedItem = newProfileViewModel;
			await Task.Delay(10);
			IsProfileSelected = true;
			IsAddButtonEnabled = true;
		},
		() => IsAddButtonEnabled);

		EditProfileCommand = Command.Factory.Create(async () =>
		{
			if (SelectedItem == null)
			{
				return;
			}

			var profileWindow = new ProfileWindow
			{
				DataContext = new ProfileWindowViewModel(SelectedItem),
			};

			await MainWindowPile.RentAsync(async owner =>
			{
				await profileWindow
					.ShowDialog(owner)
					.ConfigureAwait(true);
			}).ConfigureAwait(true);
		}
		);
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

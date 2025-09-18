using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Epoxy;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Media.Animation;
using FluentAvalonia.UI.Windowing;

using YukkuDock.Desktop.Views;

namespace YukkuDock.Desktop.ViewModels;

[ViewModel]
public partial class ProfileWindowViewModel
{
	public ProfileViewModel ProfileVm { get; }
	public int SelectedIndex { get; set; }
	public PageItem? SelectedContent { get; set; }

	public string WindowTitle { get; private set; }

	public Pile<AppWindow> WindowPile { get; } = Pile.Factory.Create<AppWindow>();

	public Pile<Frame> FramePile { get; } = Pile.Factory.Create<Frame>();

	public Well<AppWindow> WindowWell { get; } = Well.Factory.Create<AppWindow>();

	public Command? CloseCommand { get; set; }
	public bool IsClosable { get; set; } = true;

	public Command? SelectAppPathCommand { get; set; }

	public IReadOnlyList<PageItem> Pages { get; }

	public static FilePickerFileType ExeFile { get; } =
		new("Executable Files")
		{
			Patterns = ["*.exe"],
			AppleUniformTypeIdentifiers = ["com.microsoft.windows.executable"],
			MimeTypes = ["application/x-msdownload"],
		};

	bool _isLoaded = false;

	public ProfileWindowViewModel(ProfileViewModel profileVm)
	{
		ProfileVm = profileVm;
		WindowTitle = $"プロファイル設定 - {profileVm.Name} - YukkuDock";

		Pages =
		[
			new("バージョン", Symbol.Tag, new Views.SettingsPage()),
			new("プラグイン", Symbol.Library, new Views.PluginPage()),
			new("メモ", Symbol.Document, new Views.MemoPage()),
			/*
			new("テンプレート", Symbol.Home, new Views.HomePage(), false),
			new("キャラクター", Symbol.Home, new Views.HomePage(),false),
			new("レイアウト", Symbol.Home, new Views.HomePage(),false),
			*/
		];

		InitializeCommands();
	}

	private void InitializeCommands()
	{
		WindowWell.Add(
			"Loaded",
			() =>
			{
				SelectedContent = Pages[0];

				_isLoaded = true;

				return default;
			}
		);

		SelectAppPathCommand = Command.Factory.Create(async () =>
		{
			await WindowPile
				.RentAsync(async window =>
				{
					var provider = window.StorageProvider;

					var result = await provider
						.OpenFilePickerAsync(
							new() { Title = "YMM4の実行ファイルを選択", FileTypeFilter = [ExeFile] }
						)
						.ConfigureAwait(true);

					if (result is not [])
					{
						ProfileVm.AppPath = result[0].Path.AbsolutePath;
					}

					if (window.DataContext is MainWindowViewModel mwVm)
					{
						mwVm.OpenAppCommand?.ChangeCanExecute();
					}
				})
				.ConfigureAwait(true);

			//version update
			var info = FileVersionInfo.GetVersionInfo(ProfileVm.AppPath);
			Debug.WriteLine(info.FileVersion);
			if (Version.TryParse(info.FileVersion, out var version))
			{
				ProfileVm.AppVersion = version;
			}
		});

		CloseCommand = Command.Factory.Create(
			async () =>
			{
				IsClosable = false;

				//TODO: save profile settings

				// Close the window
				await WindowPile
					.RentAsync(
						(window) =>
						{
							window.Close();

							return default;
						}
					)
					.ConfigureAwait(true);

				IsClosable = true;
			},
			() => IsClosable
		);
	}


	[PropertyChanged(nameof(IsClosable))]
	[SuppressMessage("", "IDE0051")]
	private ValueTask IsClosableChangedAsync(bool value)
	{
		CloseCommand?.ChangeCanExecute();
		return default;
	}

	[PropertyChanged(nameof(SelectedContent))]
	[SuppressMessage("", "IDE0051")]
	private async ValueTask SelectedContentChangedAsync(PageItem? value)
	{
		if (value?.Content is null || !_isLoaded)
		{
			return;
		}

		await FramePile
			.RentAsync(
				(frame) =>
				{
					var type = value.Content.GetType();
					var result = frame.Navigate(type);

					if(result && frame.Content is PluginPage page){
						page.SetProfileViewModel(ProfileVm);
					}
					return default;
				}
			)
			.ConfigureAwait(true);
	}
}

using System.Diagnostics;

using Avalonia.Controls;
using Avalonia.Platform.Storage;

using Epoxy;

using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Windowing;

namespace YukkuDock.Desktop.ViewModels;

[ViewModel]
public partial class ProfileWindowViewModel
{
	public ProfileViewModel ProfileVm { get; }
	public int SelectedIndex { get; set; }
	public PageItem? SelectedContent { get; set; }

	public string WindowTitle {get; private set;}

	public Pile<AppWindow> WindowPile { get; } =
		Pile.Factory.Create<AppWindow>();

	public Command? CloseCommand { get; set; }

	public Command? SelectAppPathCommand { get; set; }

	public IReadOnlyList<PageItem> Pages { get; } = [
		new("バージョン", Symbol.Settings, new Views.SettingsPage()),
		new("プラグイン", Symbol.Library, new Views.HomePage()),
		new("メモ", Symbol.Document, new Views.MemoPage()),
		/*
		new("テンプレート", Symbol.Home, new Views.HomePage(), false),
		new("キャラクター", Symbol.Home, new Views.HomePage(),false),
		new("レイアウト", Symbol.Home, new Views.HomePage(),false),
		*/
	];

	public static FilePickerFileType ExeFile { get; } =
		new("Executable Files")
		{
			Patterns = ["*.exe"],
			AppleUniformTypeIdentifiers = ["com.microsoft.windows.executable"],
			MimeTypes = ["application/x-msdownload"],
		};

	public ProfileWindowViewModel(ProfileViewModel profileVm)
	{
		ProfileVm = profileVm;
		WindowTitle = $"プロファイル設定 - {profileVm.Name} - YukkuDock";

		SelectAppPathCommand = Command.Factory.Create(async () =>
		{
			await WindowPile.RentAsync(async window =>
			{
				var provider = window.StorageProvider;

				var result = await provider
					.OpenFilePickerAsync(
						new()
						{
							Title = "YMM4の実行ファイルを選択",
							FileTypeFilter = [ExeFile],
						}
					)
					.ConfigureAwait(true);

				if (result is not [])
				{
					ProfileVm.AppPath = result[0].Path.AbsolutePath;
				}

				if(window.DataContext is MainWindowViewModel mwVm){
					mwVm.OpenAppCommand?.ChangeCanExecute();
				}
			}).ConfigureAwait(true);

			//version update
			var info = FileVersionInfo.GetVersionInfo(ProfileVm.AppPath);
			Debug.WriteLine(info.FileVersion);
			if (Version.TryParse(info.FileVersion, out var version))
			{
				ProfileVm.AppVersion = version;
			}
		});

		CloseCommand = Command.Factory.Create(async () =>
		{
			//TODO: save profile settings

			// Close the window
			await WindowPile.RentAsync((window) =>
			{
				window.Close();
				return default;
			}).ConfigureAwait(true);
		});
	}


}

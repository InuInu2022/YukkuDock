using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

using YukkuDock.Desktop.ViewModels;

namespace YukkuDock.Desktop.Views;

public partial class PluginPage : UserControl
{
	public PluginPage()
	{
		InitializeComponent();
	}

	public PluginPage(ProfileViewModel profileVm)
		: this()
	{
		SetProfileViewModel(profileVm);
	}

	public void SetProfileViewModel(ProfileViewModel profileVm)
	{
		// ProfileViewModel を設定し、DataContext を更新
		DataContext = new PluginPageViewModel(profileVm);
	}
}
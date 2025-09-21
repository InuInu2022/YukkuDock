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
		DataContext = App.Container?.GetService<PluginPageViewModel>();
	}

	public PluginPage(ProfileViewModel profileVm)
		: this()
	{
		SetProfileViewModel(profileVm);
	}

	public void SetProfileViewModel(ProfileViewModel profileVm)
	{
		if(DataContext is PluginPageViewModel vm)
		{
			vm.ProfileVm = profileVm;
		}
	}
}
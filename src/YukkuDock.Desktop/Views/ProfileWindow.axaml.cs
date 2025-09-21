using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

using FluentAvalonia.UI.Windowing;

using YukkuDock.Desktop.ViewModels;

namespace YukkuDock.Desktop;

public partial class ProfileWindow : AppWindow
{
	public ProfileWindow()
	{
		InitializeComponent();
		DataContext = App.Container?.GetService<ProfileWindowViewModel>();
    }
}
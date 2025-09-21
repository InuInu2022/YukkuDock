using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using YukkuDock.Desktop.Container;
using YukkuDock.Desktop.ViewModels;
using YukkuDock.Desktop.Views;

namespace YukkuDock.Desktop;

public partial class App : Application
{
	public static AppContainer? Container { get; private set; }

	[MemberNotNull(nameof(Container))]
	public override void Initialize()
	{
		AvaloniaXamlLoader.Load(this);

		Container = new AppContainer();
	}

	public override void OnFrameworkInitializationCompleted()
	{
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			DisableAvaloniaDataAnnotationValidation();

			var mainWindowViewModel = Container?.GetService<MainWindowViewModel>();
			desktop.MainWindow = new MainWindow { DataContext = mainWindowViewModel };
		}

		base.OnFrameworkInitializationCompleted();
	}

	private void DisableAvaloniaDataAnnotationValidation()
	{
		// Get an array of plugins to remove
		var dataValidationPluginsToRemove = BindingPlugins
			.DataValidators.OfType<DataAnnotationsValidationPlugin>()
			.ToArray();

		// remove each entry found
		foreach (var plugin in dataValidationPluginsToRemove)
		{
			BindingPlugins.DataValidators.Remove(plugin);
		}
	}
}

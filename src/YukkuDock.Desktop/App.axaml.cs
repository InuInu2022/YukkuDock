using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

using Epoxy;

using FluentAvalonia.UI.Controls;

using NLog;

using YukkuDock.Desktop.Container;
using YukkuDock.Desktop.Services;
using YukkuDock.Desktop.ViewModels;
using YukkuDock.Desktop.Views;

namespace YukkuDock.Desktop;

public partial class App : Application
{
	public static AppContainer? Container { get; private set; }
	static readonly Logger Logger = LogManager.GetCurrentClassLogger();

	[MemberNotNull(nameof(Container))]
	public override void Initialize()
	{
		AvaloniaXamlLoader.Load(this);

		Container = new AppContainer();
	}

	public override void OnFrameworkInitializationCompleted()
	{
		RegisterGlobalExceptionHandlers();

		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			DisableAvaloniaDataAnnotationValidation();

			var mainWindowViewModel = Container?.GetService<MainWindowViewModel>();
			desktop.MainWindow = new MainWindow { DataContext = mainWindowViewModel };

			desktop.Exit += (sender, e) =>
			{
				Console.WriteLine("アプリ終了");
				// 設定保存など

				Container?.Dispose();
			};
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

	[SuppressMessage("Usage", "VSTHRD101:Avoid unsupported async delegates", Justification = "<保留中>")]
	void RegisterGlobalExceptionHandlers()
	{
		// Avalonia UI スレッド例外
		Dispatcher.UIThread.UnhandledException += async (sender, e) =>
		{
			LogError("UIThread.UnhandledException", e.Exception);
			e.Handled = true; // 強制終了を防ぐ
			await ShowErrorDialogAsync(e.Exception).ConfigureAwait(true);
		};

		// Task の非観測例外
		TaskScheduler.UnobservedTaskException += async (sender, e) =>
		{
			LogError("UnobservedTaskException", e.Exception);
			e.SetObserved();
			try
			{
				await UIThread
					.InvokeAsync(async () => await ShowErrorDialogAsync(e.Exception).ConfigureAwait(true))
					.ConfigureAwait(true);
			}
			catch (Exception ex2)
			{
				LogError("UnhandledException", ex2);
			}
		};

		// CLR の未処理例外
		AppDomain.CurrentDomain.UnhandledException += async (sender, e) =>
		{
			var ex = (Exception)e.ExceptionObject;
			LogError("UnhandledException", ex);
			try
			{
				await UIThread
					.InvokeAsync(async () => await ShowErrorDialogAsync(ex).ConfigureAwait(true))
					.ConfigureAwait(true);
			}
			catch (Exception ex2)
			{
				LogError("UnhandledException", ex2);
			}
			// ここでは Handled にできないので、最低限ログを残す
		};
	}

	static void LogError(string source, Exception ex)
	{
		Console.Error.WriteLine($"[{source}] {ex}");
		Logger.Error(ex, $"[{source}] {ex.Message}");
	}

	async Task ShowErrorDialogAsync(Exception ex)
	{
		if (
			ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop
			|| desktop.MainWindow is null
		)
		{
			return;
		}

		var dialog = new TaskDialog
		{
			Title = "エラーが発生しました",
			Content = ex.Message,
			XamlRoot = desktop.MainWindow, // これで親Windowを関連付け
			IconSource = new SymbolIconSource { Symbol = Symbol.Important },
			Buttons = { TaskDialogButton.OKButton },
		};

		await dialog.ShowAsync().ConfigureAwait(true);
	}
}

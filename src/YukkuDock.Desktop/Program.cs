using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using Avalonia;

using NLog;
using NLog.Config;
using NLog.Targets;

using Projektanker.Icons.Avalonia;
using Projektanker.Icons.Avalonia.FontAwesome;

namespace YukkuDock.Desktop;

internal sealed class Program
{
	static readonly Logger Logger = LogManager.GetCurrentClassLogger();

	// Initialization code. Don't use any Avalonia, third-party APIs or any
	// SynchronizationContext-reliant code before AppMain is called: things aren't initialized
	// yet and stuff might break.
	[STAThread]
	[SuppressMessage("Usage", "SMA0024:Enum to String")]
	[SuppressMessage("Design", "MA0076:Do not use implicit culture-sensitive ToString in interpolated strings")]
	[SuppressMessage("Usage", "SMA0021:Cast from Enum Type to Other")]
	public static int Main(string[] args)
	{
		InitLogger();
		Logger.Info("App starting...");
		var os = Environment.OSVersion;
		Logger.Info(
			$"""
			-----
			Platform: {(int)os.Platform} ({os.Platform})
			OS Version: {os.VersionString}
			CPU counts: {Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture)}
			-----
			"""
		);
		CatchExceptions();

		try
		{
			return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
		}
		catch (Exception ex)
		{
			Logger.Info("Caught exception:");
			LogExceptions(ex);
			return 1;
		}
		finally
		{
			Logger.Info("App finishing...");
		}
	}

	// Avalonia configuration, don't remove; also used by visual designer.
	public static AppBuilder BuildAvaloniaApp()
	{
		IconProvider.Current
			.Register<FontAwesomeIconProvider>();

		return AppBuilder.Configure<App>()
			.UsePlatformDetect()
			//.WithInterFont()
			.LogToTrace();
	}

	static void CatchExceptions()
	{
		AppDomain.CurrentDomain.UnhandledException += (s, e) =>
		{
			Logger.Info("UnhandledException:");
			if (e.ExceptionObject is not Exception ex)
			{
				return;
			}
			LogExceptions(ex);
		};
		TaskScheduler.UnobservedTaskException += (sender, e) =>
		{
			Logger.Info("Unobserved task exception:");
			var ex = e.Exception;
			LogExceptions(ex);
			e.SetObserved(); // 例外を監視済みとしてマーク
		};
	}

	static void LogExceptions(Exception ex)
	{
		Logger.Fatal(ex, $"Main App Error!\n{ex.Message}");
		Logger.Error($"Message: {ex.Message}");
		Logger.Error($"Exception Type: {ex.GetType().Name}");
		Logger.Error($"Stack Trace: {ex.StackTrace}");
		Logger.Error($"HResult: {ex.HResult}");

		if (ex.InnerException is not AggregateException e)
		{
			return;
		}
		foreach (var ei in e.Flatten().InnerExceptions)
		{
			Logger.Error($"-Message: {ei.Message}");
			Logger.Error($"-Exception Type: {ei.GetType().Name}");
			Logger.Error($"-Stack Trace: {ei.StackTrace}");
			Logger.Error($"-HResult: {ei.HResult}");
		}
	}


	[SuppressMessage("Usage", "SMA0040")]
	static void InitLogger()
	{
		var config = new LoggingConfiguration();

		// NLogがFileTargetのライフサイクルを管理するためusing不要
		var fileTarget = new FileTarget();
		config.AddTarget("file", fileTarget);

		fileTarget.Name = "f";
		fileTarget.FileName = "${basedir}/logs/${shortdate}.log";
		fileTarget.Layout = "${longdate} [${uppercase:${level}}] ${message}";

		var rule1 = new LoggingRule("*", LogLevel.Info, fileTarget);
		config.LoggingRules.Add(rule1);

		LogManager.Configuration = config;
	}
}

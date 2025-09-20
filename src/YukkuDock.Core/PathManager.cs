using System.Diagnostics.CodeAnalysis;

namespace YukkuDock.Core;

public static class PathManager
{
	private const string UserFolderName = "user";
	private const string PluginFolderName = "plugin";

	const string BackupFolderName = "backup";
	const string LogFolderName = "log";
	const string SettingFolderName = "setting";

	const string ProjectFolderName = "project";
	const string ResourcesFolderName = "resources";

	/// <summary>
	/// YMM4のインストールパスからプラグインフォルダを求める
	/// </summary>
	/// <param name="appPath">YMM4のインストールパス</param>
	/// <returns>プラグインフォルダ</returns>
	public static bool TryGetPluginFolder(
		string appPath,
		[NotNullWhen(true)] out DirectoryInfo? pluginDirectoryInfo
	)
	{
		pluginDirectoryInfo = null;
		if (string.IsNullOrWhiteSpace(appPath))
			return false;

		// 実行ファイルのディレクトリ取得
		var baseDir = Path.GetDirectoryName(appPath);
		if (baseDir is null)
			return false;

		// user\plugin フォルダのパスを結合
		var pluginDir = Path.Combine(baseDir, UserFolderName, PluginFolderName);
		try
		{
			pluginDirectoryInfo = new DirectoryInfo(pluginDir);
		}
		catch (System.Exception)
		{
			return false;
		}
		return pluginDirectoryInfo.Exists;
	}
}

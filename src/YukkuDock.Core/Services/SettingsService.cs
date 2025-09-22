using System.Text.Json;
using YukkuDock.Core.Models;

namespace YukkuDock.Core.Services;

/// <summary>
/// アプリ全体の設定管理サービス実装
/// </summary>
public partial class SettingsService : ISettingsService
{
	readonly string _settingsPath;

	public SettingsService()
	{
		var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
		_settingsPath = Path.Combine(appData, "YukkuDock", "settings.json");
	}

	/// <summary>
	/// 設定ファイルの安全な読込。失敗時はSuccess=false。
	/// </summary>
	public async Task<TryAsyncResult<Settings>> TryLoadAsync()
	{
		try
		{
			if (!File.Exists(_settingsPath))
			{
				return new(false, null, new FileNotFoundException("Settings file not found", _settingsPath));
			}

			var json = await File.ReadAllTextAsync(_settingsPath).ConfigureAwait(false);
			var settings = JsonSerializer
				.Deserialize(json, SettingsJsonContext.Default.Settings);
			return settings is not null
				? new(true, settings)
				: new(false, null);
		}
		catch (Exception ex)
		{
			return new(false, null, ex);
		}
	}

	/// <summary>
	/// 設定ファイルの安全な保存。失敗時はSuccess=false。
	/// </summary>
	public async Task<TryAsyncResult<bool>> TrySaveAsync(Settings settings)
	{
		try
		{
			var dir = Path.GetDirectoryName(_settingsPath);
			if (!Directory.Exists(dir))
				Directory.CreateDirectory(dir!);

			var json = JsonSerializer.Serialize(settings, SettingsJsonContext.Default.Settings);
			await File.WriteAllTextAsync(_settingsPath, json).ConfigureAwait(false);
			return new(true, true);
		}
		catch (Exception ex)
		{
			return new(false, false, ex);
		}
	}
}

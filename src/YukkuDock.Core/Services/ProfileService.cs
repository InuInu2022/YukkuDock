using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using YukkuDock.Core.Models;

namespace YukkuDock.Core.Services;

/// <summary>
/// プロファイル管理サービスの実装
/// </summary>
public class ProfileService : IProfileService
{
	/// <summary>
	/// プロファイルの安全な読込。失敗時はSuccess=false。
	/// </summary>
	public async Task<TryAsyncResult<Profile>> TryLoadAsync(string profileFolderPath)
	{
		try
		{
			if (!Directory.Exists(profileFolderPath))
			{
				return new(false, null);
			}

			var profilePath = Path.Combine(profileFolderPath, "profile.json");
			if (!File.Exists(profilePath))
			{
				return new(false, null);
			}

			var json = await File.ReadAllTextAsync(profilePath).ConfigureAwait(false);
			var profile = JsonSerializer.Deserialize(json, ProfileJsonContext.Default.Profile);
			return profile is not null ? new(true, profile) : new(false, null);
		}
		catch
		{
			return new(false, null);
		}
	}

	/// <summary>
	/// プロファイルの安全な保存。失敗時はSuccess=false。
	/// </summary>
	public async Task<TryAsyncResult<bool>> TrySaveAsync(Profile profile, string profileFolderPath)
	{
		try
		{
			var profilePath = Path.Combine(profileFolderPath, "profile.json");
			if (!Directory.Exists(profileFolderPath))
				Directory.CreateDirectory(profileFolderPath);
			var json = JsonSerializer.Serialize(profile, ProfileJsonContext.Default.Profile);
			await File.WriteAllTextAsync(profilePath, json).ConfigureAwait(false);
			return new(true, true);
		}
		catch
		{
			return new(false, false);
		}
	}
}

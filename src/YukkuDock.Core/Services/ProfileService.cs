using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using YukkuDock.Core.Models;

namespace YukkuDock.Core.Services;

/// <summary>
/// プロファイル管理サービスの実装
/// </summary>
public class ProfileService : IProfileService
{
	readonly string profilesRootPath;

	public ProfileService()
	{
		var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
		profilesRootPath = Path.Combine(appData, "YukkuDock", "Profiles");
	}

	public IEnumerable<Guid> GetAllProfileIds()
	{
		if (!Directory.Exists(profilesRootPath))
			yield break;

		foreach (var dir in Directory.EnumerateDirectories(profilesRootPath))
		{
			if (Guid.TryParse(Path.GetFileName(dir), out var id))
				yield return id;
		}
	}

	public string GetProfileFolder(Guid id) => Path.Combine(profilesRootPath, id.ToString());

	// Profile保存フォルダ直下にBackup/GUID/を作成
	public string GetProfileBackupFolder(Guid id) => Path.Combine(profilesRootPath, "Backup", id.ToString());

	public async Task<TryAsyncResult<Profile>> TryLoadAsync(Guid id)
	{
		var folder = GetProfileFolder(id);
		var profilePath = Path.Combine(folder, "profile.json");
		try
		{
			if (!File.Exists(profilePath))
				return new(false, null);

			var json = await File.ReadAllTextAsync(profilePath).ConfigureAwait(false);
			var profile = JsonSerializer.Deserialize(json, ProfileJsonContext.Default.Profile);
			return profile is not null ? new(true, profile) : new(false, null);
		}
		catch
		{
			return new(false, null);
		}
	}

	public async Task<TryAsyncResult<IReadOnlyList<Profile>>> TryLoadAllAsync()
	{
		var profiles = new List<Profile>();
		try
		{
			foreach (var id in GetAllProfileIds())
			{
				var result = await TryLoadAsync(id).ConfigureAwait(false);
				if (result.Success && result.Value is not null)
				{
					profiles.Add(result.Value);
				}
			}
		}
		catch
		{
			return new(false, null);
		}
		return new(true, profiles);
	}

	public async Task<TryAsyncResult<bool>> TrySaveAsync(Profile profile)
	{
		var folder = GetProfileFolder(profile.Id);
		var profilePath = Path.Combine(folder, "profile.json");
		try
		{
			if (!Directory.Exists(folder))
				Directory.CreateDirectory(folder);
			var json = JsonSerializer.Serialize(profile, ProfileJsonContext.Default.Profile);
			await File.WriteAllTextAsync(profilePath, json).ConfigureAwait(false);
			return new(true, true);
		}
		catch
		{
			return new(false, false);
		}
	}

	public async Task<TryAsyncResult<bool>> TryDeleteAsync(Profile profile) {
		var folder = GetProfileFolder(profile.Id);
		try
		{
			if (Directory.Exists(folder))
			{
				var result = await RecycleBinManager.TryMoveAsync(folder).ConfigureAwait(false);
				return new(result.Success, result.Success);
			}
			return new(false, false);
		}
		catch
		{
			return new(false, false);
		}
	}
}
